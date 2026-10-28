using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Output;

/// <summary>
/// Pins CGR-215: <see cref="TraceTransferEventWriter"/> shows byte 0x7F as "." in its text
/// column, as curl's trace dump does (it shows only 0x20 to 0x7E).
/// </summary>
[TestClass]
public sealed class Cgr215Tests
{
    [TestMethod]
    public void ReportDataSent_TextOnly_ShowsDeleteByteAsDot()
    {
        MemoryStream output = new();
        TraceTransferEventWriter writer = new(output, TraceDumpFormat.TextOnly, false, TimeProvider.System, TlsBackend.Schannel);

        writer.ReportDataSent([(byte)'~', 0x7F, (byte)'a']);

        string rendered = Encoding.ASCII.GetString(output.ToArray());
        Assert.IsTrue(rendered.Contains("0000: ~.a", StringComparison.Ordinal), rendered);
    }
}
