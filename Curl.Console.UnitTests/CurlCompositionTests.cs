using System.Reflection;

using Curl.Cli;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Protocol.File;

namespace Curl.Console;

/// <summary>
/// Pins the composition root: which handlers the executable registers, how the network
/// transports are wired, and that the production composition performs a <c>file://</c>
/// transfer end to end. No test here opens a socket.
/// </summary>
[TestClass]
public sealed class CurlCompositionTests
{
    [TestMethod]
    public void CreateProtocolHandlers_ServesFileThroughFileProtocolHandler()
    {
        IReadOnlyList<IProtocolHandler> handlers = CurlComposition.CreateProtocolHandlers();

        IProtocolHandler handler = handlers.Single();
        Assert.IsInstanceOfType<FileProtocolHandler>(handler);
        CollectionAssert.Contains(handler.SupportedSchemes.ToArray(), "file");
    }

    [TestMethod]
    public async Task CreateRunner_FileUrlOfTemporaryFile_WritesItsBytesToStandardOutput()
    {
        string path = Path.Combine(Path.GetTempPath(), $"curl-bl068-{Guid.NewGuid():N}.bin");
        byte[] content = [0, 1, 2, 13, 10, 255, (byte)'x'];
        await System.IO.File.WriteAllBytesAsync(path, content);

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();

            int exitCode = await CurlComposition.CreateRunner(standardOutput, standardError)
                .RunAsync([new Uri(path).AbsoluteUri]);

            Assert.AreEqual(0, exitCode);
            CollectionAssert.AreEqual(content, standardOutput.ToArray());
            Assert.AreEqual(0, standardError.Length);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [TestMethod]
    public void CreateTransports_BothConnectors_ShareOneSystemDnsResolverAndTimeProviderSystem()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.IsInstanceOfType<SystemDnsResolver>(transports.DnsResolver);
        Assert.AreSame(TimeProvider.System, transports.TimeProvider);
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(transports.TcpConnector));
        Assert.AreSame(transports.DnsResolver, CapturedDependency<IDnsResolver>(transports.UdpDatagramConnector));
        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.TcpConnector));
        Assert.AreSame(TimeProvider.System, CapturedDependency<TimeProvider>(transports.UdpDatagramConnector));
    }

    [TestMethod]
    public void CreateTransports_TcpConnector_ReceivesTcpDialerAndSecureSslStreamTlsProvider()
    {
        CurlTransports transports = CurlComposition.CreateTransports(NoOptions());

        Assert.AreSame(transports.TcpDialer, CapturedDependency<ITcpDialer>(transports.TcpConnector));
        Assert.AreSame(transports.TlsProvider, CapturedDependency<ITlsProvider>(transports.TcpConnector));
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
        Assert.IsFalse(transports.TlsClientOptions.Insecure);
    }

    [TestMethod]
    public void CreateTransports_InsecureCaCertificateAndTlsv13_SslStreamTlsProviderReceivesMappedOptions()
    {
        CommandLineOptions options = Parse("-k", "--cacert", "x.pem", "--tlsv1.3", "gophers://example.com/");

        CurlTransports transports = CurlComposition.CreateTransports(options);

        TlsClientOptions expected = new(Insecure: true, MinimumVersion: TlsMinimumVersion.Tls13, CaCertificateFile: "x.pem");
        Assert.AreEqual(expected, transports.TlsClientOptions);
        Assert.AreSame(transports.TlsClientOptions, CapturedDependency<TlsClientOptions>(transports.TlsProvider));
    }

    [TestMethod]
    public void CreateTransports_EachCall_BuildsItsOwnResolver()
    {
        Assert.AreNotSame(
            CurlComposition.CreateTransports(NoOptions()).DnsResolver,
            CurlComposition.CreateTransports(NoOptions()).DnsResolver);
    }

    private static CommandLineOptions NoOptions() => Parse("gophers://example.com/");

    /// <summary>
    /// Parses <paramref name="arguments" /> as if every path exists, so <c>--cacert</c>
    /// accepts a file name without touching the disk.
    /// </summary>
    private static CommandLineOptions Parse(params string[] arguments)
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(arguments, _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>
    /// Reads the one dependency of type <typeparamref name="T" /> that <paramref name="owner" />
    /// holds in a private field. The networking types keep what they were constructed with
    /// private, so this is how a test checks the wiring without opening a socket.
    /// </summary>
    private static T CapturedDependency<T>(object owner)
        where T : class
    {
        FieldInfo field = owner.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.FieldType == typeof(T));
        return (T)field.GetValue(owner)!;
    }
}
