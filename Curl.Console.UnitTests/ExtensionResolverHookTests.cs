using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Resolution;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Pins that the Console passes the resolver hook an option group's extension options activate down to the
/// engine (ADR-1012): a transfer with the option given dials the address the hook's resolver answers, the
/// option beside <c>--doh-url</c> or a second resolver option is a command-line error with exit 2, and a
/// transfer without one keeps curl's resolver.
/// </summary>
[TestClass]
public sealed class ExtensionResolverHookTests
{
    private static readonly ExtensionOption DotOption = new("test-dot", ExtensionOptionArgument.Value, "Test DNS-over-TLS");
    private static readonly ExtensionOption DoqOption = new("test-doq", ExtensionOptionArgument.None, "Test DNS-over-QUIC");
    private static readonly IPAddress CurlAddress = IPAddress.Parse("10.0.0.9");
    private static readonly IPAddress HookAddress = IPAddress.Parse("10.0.0.7");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly RecordingRefusingTcpDialer dialer = new();
    private readonly RecordingDnsResolver curlResolver = new(CurlAddress);
    private readonly RecordingDnsResolver hookResolver = new(HookAddress);

    private string StandardErrorText => Encoding.UTF8.GetString(standardError.ToArray());

    [TestMethod]
    public async Task RunAsync_ResolverOptionGiven_LooksTheHostUpWithTheExtensionsResolver()
    {
        int exitCode = await RunAsync("-s", "--test-dot", "dot.example", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(HookAddress, 47114) }, dialer.Dialled);
        CollectionAssert.Contains(hookResolver.LookedUp, "site.example");
        Assert.IsEmpty(curlResolver.LookedUp);
    }

    [TestMethod]
    public async Task RunAsync_ResolverOptionAndDohUrlGiven_ExitsTwoNamingBothOptions()
    {
        int exitCode = await RunAsync("-sS", "--test-dot", "dot.example", "--doh-url", "https://doh.example/dns-query", "http://site.example:47114/");

        Assert.AreEqual(2, exitCode);
        Assert.AreEqual(
            "curl: the options --test-dot and --doh-url each supply the resolver a transfer looks host names up with; give only one of them\n"
                + "curl: try 'curl --help' or 'curl --manual' for more information\n",
            StandardErrorText.ReplaceLineEndings("\n"));
        Assert.IsEmpty(dialer.Dialled);
        Assert.IsEmpty(hookResolver.LookedUp);
    }

    [TestMethod]
    public async Task RunAsync_TwoResolverOptionsGiven_ExitsTwoNamingBothOptions()
    {
        int exitCode = await RunAsync("-sS", "--test-dot", "dot.example", "--test-doq", "http://site.example:47114/");

        Assert.AreEqual(2, exitCode);
        Assert.StartsWith("curl: the options --test-dot and --test-doq each supply the resolver", StandardErrorText);
        Assert.IsEmpty(dialer.Dialled);
    }

    [TestMethod]
    public async Task RunAsync_NoExtensionOptionGiven_LooksTheHostUpWithCurlsResolver()
    {
        int exitCode = await RunAsync("-s", "http://site.example:47114/");

        Assert.AreEqual((int)CurlExitCode.CouldntConnect, exitCode);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(CurlAddress, 47114) }, dialer.Dialled);
        Assert.IsEmpty(hookResolver.LookedUp);
    }

    [TestMethod]
    public void ExtensionResolverHooksOf_NoExtensionOptionGiven_PassesNoResolverToTheEngine()
    {
        ResolverHooks hooks = CurlComposition.ExtensionResolverHooksOf(new CommandLineOptions(), [ActivatedByDot, ActivatedByDoq]);

        Assert.IsNull(hooks.Hook);
        Assert.AreSame(curlResolver, hooks.ResolverOver(curlResolver));
        Assert.IsNull(CurlComposition.ExtensionResolverOf(hooks));
        Assert.IsNull(CurlComposition.ExtensionResolverOf(null));
        Assert.IsNull(CurlComposition.ExtensionResolverHooksOf(new CommandLineOptions()).Hook);
    }

    [TestMethod]
    public void ExtensionResolverOf_HookActivated_GivesTheHooksResolver()
    {
        ResolverHooks hooks = CurlComposition.ExtensionResolverHooksOf(new CommandLineOptions(), [_ => new ResolverReplacingHook(DotOption, hookResolver)]);

        Assert.AreSame(hookResolver, CurlComposition.ExtensionResolverOf(hooks)!(curlResolver));
    }

    private IResolverHook? ActivatedByDot(ExtensionOptionValues values) =>
        values.ValueOf(DotOption) is not null ? new ResolverReplacingHook(DotOption, hookResolver) : null;

    private IResolverHook? ActivatedByDoq(ExtensionOptionValues values) =>
        values.ValueOf(DoqOption) is not null ? new ResolverReplacingHook(DoqOption, hookResolver) : null;

    private Task<int> RunAsync(params string[] arguments) =>
        CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                dialer,
                curlResolver,
                new NoDatagramConnector(),
                extensionOptions: [DotOption, DoqOption],
                extensionResolverHookActivators: [ActivatedByDot, ActivatedByDoq])
            .RunAsync(arguments);

    private sealed class ResolverReplacingHook(ExtensionOption option, IDnsResolver resolver) : IResolverHook
    {
        public ExtensionOption Option => option;

        public IDnsResolver ResolverOver(IDnsResolver curlResolver) => resolver;
    }

    /// <summary>A resolver answering every name with one address and recording each name it was asked.</summary>
    private sealed class RecordingDnsResolver(IPAddress address) : IDnsResolver
    {
        private readonly List<string> lookedUp = [];

        public List<string> LookedUp
        {
            get
            {
                lock (lookedUp)
                {
                    return [.. lookedUp];
                }
            }
        }

        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken)
        {
            lock (lookedUp)
            {
                lookedUp.Add(host);
            }

            return ValueTask.FromResult<IReadOnlyList<IPAddress>>([address]);
        }
    }

    /// <summary>A datagram connector no test reaches.</summary>
    private sealed class NoDatagramConnector : IDatagramConnector
    {
        public ValueTask<DatagramOpenResult> OpenAsync(string host, int port, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No datagram channel is opened.");
    }

    /// <summary>A dialer that records each address and refuses it.</summary>
    private sealed class RecordingRefusingTcpDialer : ITcpDialer
    {
        private readonly List<IPEndPoint> dialled = [];

        public List<IPEndPoint> Dialled
        {
            get
            {
                lock (dialled)
                {
                    return [.. dialled];
                }
            }
        }

        public ValueTask<DialedTcpConnection> DialAsync(IPEndPoint endPoint, CancellationToken cancellationToken)
        {
            lock (dialled)
            {
                dialled.Add(endPoint);
            }

            throw new SocketException((int)SocketError.ConnectionRefused);
        }

        public ValueTask<DialedTcpConnection> DialFromAsync(IPEndPoint endPoint, IPEndPoint localEndPoint, int localPortCount, CancellationToken cancellationToken) =>
            DialAsync(endPoint, cancellationToken);

        public ValueTask<IConnection> DialUnixSocketAsync(UnixSocketAddress address, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No Unix socket is dialled.");
    }
}
