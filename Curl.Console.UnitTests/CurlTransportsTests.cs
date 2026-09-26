using Curl.Cli;

namespace Curl.Console;

/// <summary>
/// Pins the <c>with</c> behaviour of the <see cref="CurlTransports" /> record: a copy holds
/// every property it names and carries over every property it does not.
/// </summary>
[TestClass]
public sealed class CurlTransportsTests
{
    [TestMethod]
    public void CurlTransports_WithExpressionNamingEveryProperty_ReplacesEachProperty()
    {
        CurlTransports original = CurlComposition.CreateTransports(NoOptions());
        CurlTransports replacement = CurlComposition.CreateTransports(NoOptions());
        TimeProvider replacementTimeProvider = new ReplacementTimeProvider();

        CurlTransports copy = original with
        {
            DnsResolver = replacement.DnsResolver,
            TimeProvider = replacementTimeProvider,
            TcpDialer = replacement.TcpDialer,
            TlsClientOptions = replacement.TlsClientOptions with { Insecure = true },
            TlsProvider = replacement.TlsProvider,
            TcpConnector = replacement.TcpConnector,
            UdpDatagramConnector = replacement.UdpDatagramConnector,
        };

        Assert.AreSame(replacement.DnsResolver, copy.DnsResolver);
        Assert.AreSame(replacementTimeProvider, copy.TimeProvider);
        Assert.AreSame(replacement.TcpDialer, copy.TcpDialer);
        Assert.IsTrue(copy.TlsClientOptions.Insecure);
        Assert.AreSame(replacement.TlsProvider, copy.TlsProvider);
        Assert.AreSame(replacement.TcpConnector, copy.TcpConnector);
        Assert.AreSame(replacement.UdpDatagramConnector, copy.UdpDatagramConnector);
        Assert.AreSame(TimeProvider.System, original.TimeProvider);
        Assert.IsFalse(original.TlsClientOptions.Insecure);
    }

    [TestMethod]
    public void CurlTransports_WithExpressionNamingOneProperty_CopiesTheRest()
    {
        CurlTransports original = CurlComposition.CreateTransports(NoOptions());
        CurlTransports replacement = CurlComposition.CreateTransports(NoOptions());

        CurlTransports copy = original with { DnsResolver = replacement.DnsResolver };

        Assert.AreSame(replacement.DnsResolver, copy.DnsResolver);
        Assert.AreSame(original.TimeProvider, copy.TimeProvider);
        Assert.AreSame(original.TcpDialer, copy.TcpDialer);
        Assert.AreSame(original.TlsClientOptions, copy.TlsClientOptions);
        Assert.AreSame(original.TlsProvider, copy.TlsProvider);
        Assert.AreSame(original.TcpConnector, copy.TcpConnector);
        Assert.AreSame(original.UdpDatagramConnector, copy.UdpDatagramConnector);
    }

    private static CommandLineOptions NoOptions()
    {
        CommandLineParseResult parsed = CommandLineParser.Parse(["gophers://example.com/"], _ => true);
        Assert.IsTrue(parsed.IsAccepted);
        return parsed.Options;
    }

    /// <summary>
    /// A clock that behaves like <see cref="TimeProvider.System" /> but is a different
    /// instance, so a test can tell a replaced <see cref="CurlTransports.TimeProvider" />
    /// from the original.
    /// </summary>
    private sealed class ReplacementTimeProvider : TimeProvider;
}
