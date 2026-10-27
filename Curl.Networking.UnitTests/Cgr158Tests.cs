using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-158: a host name the DoH resolver cannot put in a query is curl 8.22.0's exit 27
/// <c>Failed to encode DoH packet [N]</c> with nothing sent, not exit 6 (measured on curl 8.22.0 Schannel:
/// a 64-byte label and <c>foo..test</c> give <c>[1]</c>, a 310-byte name <c>[13]</c>).
/// </summary>
[TestClass]
public sealed class Cgr158Tests
{
    private static readonly Uri DohUrl = new("http://127.0.0.1:21800/dns-query");

    [TestMethod]
    [DataRow("label64", DnsLookupFailure.DohBadLabel)]
    [DataRow("emptylabel", DnsLookupFailure.DohBadLabel)]
    [DataRow("name310", DnsLookupFailure.DohNameTooLong)]
    public async Task ResolveWithFailureReasonAsync_NameThatCannotBeEncoded_ReportsTheEncodingFailureAndSendsNothing(string name, DnsLookupFailure expected)
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, DohUrl);

        var resolution = await resolver.ResolveWithFailureReasonAsync(HostNamed(name), CancellationToken.None);

        Assert.AreEqual(expected, resolution.Failure);
        Assert.IsEmpty(resolution.Addresses);
        Assert.IsEmpty(connector.Opened);
    }

    [TestMethod]
    [DataRow(DnsLookupFailure.DohBadLabel, "Failed to encode DoH packet [1]")]
    [DataRow(DnsLookupFailure.DohNameTooLong, "Failed to encode DoH packet [13]")]
    public void Describe_DohEncodingFailure_IsExit27WithCurlsMessage(DnsLookupFailure failure, string expectedMessage)
    {
        var (exitCode, message) = NameResolutionFailure.Describe(CurlExitCode.CouldntResolveHost, "host", "foo..test", 21802, failure);

        Assert.AreEqual(CurlExitCode.OutOfMemory, exitCode);
        Assert.AreEqual(expectedMessage, message);
    }

    private static string HostNamed(string name) => name switch
    {
        "label64" => new string('a', 64) + ".test",
        "emptylabel" => "foo..test",
        _ => string.Concat(Enumerable.Repeat(new string('b', 50) + ".", 6)) + "test",
    };
}
