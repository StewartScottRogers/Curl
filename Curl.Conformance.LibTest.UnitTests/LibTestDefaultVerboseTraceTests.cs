using System.Text;
using Curl.Api.Callbacks;

namespace Curl.Conformance.LibTest;

[TestClass]
public sealed class LibTestDefaultVerboseTraceTests
{
    [TestMethod]
    public void Constructor_NullOutput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new LibTestDefaultVerboseTrace(null!));

    [TestMethod]
    public void Trace_TextAndHeaders_PrefixesEachLineAsLibcurlsDefaultVerboseOutputDoes()
    {
        var output = new StringWriter();
        var trace = new LibTestDefaultVerboseTrace(output);

        int text = trace.Trace(CurlInfoType.Text, Encoding.Latin1.GetBytes("Connection #0 to host localhost:9 left intact\n"));
        int headerOut = trace.Trace(CurlInfoType.HeaderOut, Encoding.Latin1.GetBytes("GET / HTTP/1.1\r\nHost: x\r\n\r\n"));
        int headerIn = trace.Trace(CurlInfoType.HeaderIn, Encoding.Latin1.GetBytes("HTTP/1.1 200 OK\r\nlast"));

        Assert.AreEqual(0, text + headerOut + headerIn);
        Assert.AreEqual(
            "* Connection #0 to host localhost:9 left intact\n> GET / HTTP/1.1\r\n> Host: x\r\n> \r\n< HTTP/1.1 200 OK\r\n< last",
            output.ToString());
    }

    [TestMethod]
    public void Trace_DataAndEmptyText_WritesNothing()
    {
        var output = new StringWriter();
        var trace = new LibTestDefaultVerboseTrace(output);

        int data = trace.Trace(CurlInfoType.DataIn, "body"u8);
        int empty = trace.Trace(CurlInfoType.Text, ReadOnlySpan<byte>.Empty);

        Assert.AreEqual(0, data + empty);
        Assert.AreEqual(string.Empty, output.ToString());
    }
}
