using System.Text;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>CGR-285 (BL-3814): which DoH trace lines the run's <c>--trace-config</c> components let through.</summary>
[TestClass]
public sealed class Cgr285Tests
{
    [TestMethod]
    [DataRow(null, true, true)]
    [DataRow("doh", false, true)]
    [DataRow("dns", true, false)]
    [DataRow("doh,dns", true, true)]
    [DataRow("all", true, true)]
    [DataRow("network", true, true)]
    [DataRow("http", true, true)]
    public void DohSubTransferEventsOf_Components_GateFilterAndSubTransferLines(string? components, bool writesFilter, bool writesSubTransfer)
    {
        EngineTraceComponents? traces = components is null ? null : new(new HashSet<string>(components.Split(','), StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
        MemoryStream output = new();
        DohSubTransferEvents events = EngineComposition.DohSubTransferEventsOf(new VerboseTransferEventWriter(output, writesDataLines: true, TlsBackend.Schannel), traces);

        events.ReportInfo("[DNS] added");
        events.ReportInfo("connected");
        string written = Encoding.UTF8.GetString(output.ToArray());

        Assert.AreEqual(writesFilter, written.Contains("[DNS] added", StringComparison.Ordinal), written);
        Assert.AreEqual(writesSubTransfer, written.Contains("[DoH] connected", StringComparison.Ordinal), written);
    }
}
