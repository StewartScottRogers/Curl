namespace Curl.Extension.CodeGeneration.UnitTests;

[TestClass]
public sealed class CodeGenerationTextFormTests
{
    private const string Form = "--b\r\nContent-Disposition: form-data; name=\"n\"\r\n\r\nv\r\n--b--\r\n";

    [TestMethod]
    public void From_ASeekableMultipartForm_GivesItsBytesAndLeavesTheStreamAtItsStart()
    {
        var stream = new MemoryStream(GeneratedRequestTests.Utf8(Form));
        var request = GeneratedRequest.From(Context(new StreamBody(stream, stream.Length, "multipart/form-data; boundary=b")));

        Assert.AreEqual(Form, request.BodyText);
        Assert.AreEqual(0, stream.Position);
        Assert.AreEqual("multipart/form-data; boundary=b", request.Headers.Single().Value);
    }

    [TestMethod]
    public void From_AStreamPartWayRead_IsPutBackWhereItWas()
    {
        var stream = new MemoryStream(GeneratedRequestTests.Utf8(Form)) { Position = 5 };

        Assert.AreEqual(Form, GeneratedRequest.From(Context(new StreamBody(stream, stream.Length, "multipart/form-data; boundary=b"))).BodyText);
        Assert.AreEqual(5, stream.Position);
    }

    [TestMethod]
    public void From_AStreamThatCannotBeReadAhead_GivesNoBody()
    {
        var unseekable = new NonSeekableStream();
        var tooLarge = new MemoryStream(new byte[GeneratedRequest.MaxReadAheadBytes + 1]);

        Assert.IsNull(GeneratedRequest.From(Context(new StreamBody(unseekable, null, "multipart/form-data"))).Body);
        Assert.IsNull(GeneratedRequest.From(Context(new StreamBody(tooLarge, tooLarge.Length, "multipart/form-data"))).Body);
        Assert.IsNull(GeneratedRequest.From(Context(new StreamBody(new MemoryStream(), 0, "multipart/form-data"))).Body);
    }

    [TestMethod]
    public void From_AWriteOnlySeekableStream_GivesNoBody()
    {
        Assert.IsNull(GeneratedRequest.From(Context(new StreamBody(new WriteOnlyStream(), 3, "multipart/form-data"))).Body);
    }

    [TestMethod]
    public void For_ABodyWithLineBreaks_IsWrittenAsBase64SoTheLineBreaksSurvive()
    {
        var script = PowerShellScript.For([CodeGenerationExtensionTests.Request("POST", GeneratedRequestTests.Utf8(Form))]);

        StringAssert.Contains(script, "FromBase64String('" + Convert.ToBase64String(GeneratedRequestTests.Utf8(Form)) + "')");
    }

    private static TransferContext Context(HttpRequestBody body) =>
        new() { Url = CurlUrl.Parse("http://h/"), Output = Stream.Null, Http = new HttpRequestOptions { Body = body } };

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }

    private sealed class WriteOnlyStream : MemoryStream
    {
        public override bool CanRead => false;

        public override long Length => 3;
    }
}
