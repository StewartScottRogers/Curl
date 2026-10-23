using Curl.Conformance.Differential.Libcurl.Printf;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Printf;

[TestClass]
public sealed class LibcurlPrintfLineWriterTests
{
    [TestMethod]
    public void Result_CountAndBuffer_WritesTheCountAndAtMostTheBufferLessOneBytes()
    {
        Assert.AreEqual("2 6869", LibcurlPrintfLineWriter.Result(2, "hi\0x"u8));
        Assert.AreEqual("9 616263", LibcurlPrintfLineWriter.Result(9, "abcd"u8));
        Assert.AreEqual("-1 ", LibcurlPrintfLineWriter.Result(-1, "ab"u8));
        Assert.AreEqual("3 ", LibcurlPrintfLineWriter.Result(3, []));
    }

    [TestMethod]
    public void Failure_Exception_WritesItsTypeName() =>
        Assert.AreEqual("EXC FormatException", LibcurlPrintfLineWriter.Failure(new FormatException()));
}
