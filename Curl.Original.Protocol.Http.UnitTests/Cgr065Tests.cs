using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-065 (BL-3594): a <c>-X</c> method goes on the request line as its bytes in the
/// command line's encoding, as curl copies it from its <c>argv</c> (measured on curl 8.18.0,
/// Linux: <c>-X 中</c> sends <c>E4 B8 AD /a HTTP/1.1</c>).
/// </summary>
[TestClass]
public sealed class Cgr065Tests
{
    [TestMethod]
    public void Format_NonAsciiMethodWithUtf8CommandLine_SendsTheMethodsUtf8Bytes()
    {
        HttpRequestOptions options = new() { CustomMethod = "中", CommandLineTextEncoding = Encoding.UTF8 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://127.0.0.1/a"), options);

        Assert.StartsWith("\u00E4\u00B8\u00AD /a HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_MethodHoldingAnEscapedInvalidByte_SendsThatByte()
    {
        HttpRequestOptions options = new() { CustomMethod = "G\uDCFFT", CommandLineTextEncoding = Encoding.UTF8 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://127.0.0.1/a"), options);

        Assert.StartsWith("GÿT /a HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void Format_AsciiMethod_SendsItUnchanged()
    {
        HttpRequestOptions options = new() { CustomMethod = "PATCH", CommandLineTextEncoding = Encoding.UTF8 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://127.0.0.1/a"), options);

        Assert.StartsWith("PATCH /a HTTP/1.1\r\n", Encoding.Latin1.GetString(head));
    }
}
