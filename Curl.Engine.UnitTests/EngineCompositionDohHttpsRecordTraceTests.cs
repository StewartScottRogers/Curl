using System.Net.Sockets;

using Curl.Networking;

namespace Curl.Engine;

/// <summary>
/// Pins which of the HTTPS-RR DoH query's trace lines each <c>--trace-config</c> turns on, as curl
/// 8.22.0 gates them (ADR-0727's BL-2979 amendment).
/// </summary>
[TestClass]
public sealed class EngineCompositionDohHttpsRecordTraceTests
{
    [TestMethod]
    [DataRow("dns", false, false, DisplayName = "dns")]
    [DataRow("doh", true, false, DisplayName = "doh")]
    [DataRow("dns,doh", true, true, DisplayName = "dns,doh")]
    [DataRow("network", true, true, DisplayName = "network")]
    [DataRow("all", true, true, DisplayName = "all")]
    [DataRow("tcp", false, false, DisplayName = "tcp")]
    public void HttpsRecordTraceOf_TheComponents_GatesTheDohAndDnsLines(string components, bool dohLines, bool dnsLines)
    {
        var traces = new EngineTraceComponents(new HashSet<string>(components.Split(','), StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

        var gate = EngineComposition.HttpsRecordTraceOf(traces);

        Assert.AreEqual(new DohHttpsRecordTrace(dohLines, dnsLines), gate);
    }

    [TestMethod]
    public void CreateDohResolver_WithTraces_GivesTheResolverTheirGate()
    {
        var traces = new EngineTraceComponents(new HashSet<string>(["doh"], StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));

        var resolver = (DohDnsResolver)EngineComposition.CreateDohResolver("https://127.0.0.1/dns-query", new EndPointScriptedConnector(), AddressFamily.Unspecified, traces: traces);

        Assert.AreEqual(new DohHttpsRecordTrace(true, false), resolver.HttpsRecordTrace);
    }

    [TestMethod]
    public void CreateDohResolver_WithoutTraces_WritesEveryLine()
    {
        var resolver = (DohDnsResolver)EngineComposition.CreateDohResolver("https://127.0.0.1/dns-query", new EndPointScriptedConnector(), AddressFamily.Unspecified);

        Assert.AreEqual(DohHttpsRecordTrace.All, resolver.HttpsRecordTrace);
    }
}
