using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Libcurl.Mime;

namespace Libcurl.UnitTests.Mime.Mime;

[TestClass]
public sealed class CurlMimePartTests
{
    [TestMethod]
    public void TextSetters_PassTheirValueToTheEnginePart()
    {
        var (part, engine) = NewPart();

        Assert.AreEqual(CurlCode.Ok, part.SetName("field"));
        Assert.AreEqual(CurlCode.Ok, part.SetFileName("a.txt"));
        Assert.AreEqual(CurlCode.Ok, part.SetType("text/plain"));
        Assert.AreEqual(CurlCode.Ok, part.SetEncoder("base64"));
        Assert.AreEqual(CurlCode.Ok, part.SetFileData("dir/a.txt"));

        Assert.AreEqual("field", engine.Name);
        Assert.AreEqual("a.txt", engine.FileName);
        Assert.AreEqual("text/plain", engine.Type);
        Assert.AreEqual("base64", engine.Encoder);
        Assert.AreEqual("dir/a.txt", engine.FilePath);
        CollectionAssert.AreEqual(new[] { "SetName", "SetFileName", "SetType", "SetEncoder", "SetFileData" }, engine.Calls);
    }

    [TestMethod]
    public void Setters_AnswerTheEnginesCode()
    {
        var (part, engine) = NewPart();
        engine.Result = CurlCode.BadFunctionArgument;

        Assert.AreEqual(CurlCode.BadFunctionArgument, part.SetEncoder("rot13"));
    }

    [TestMethod]
    public void SetData_Bytes_PassesTheBytesAndNullPassesNull()
    {
        var (part, engine) = NewPart();
        byte[] bytes = [1, 2, 3];

        part.SetData(bytes);
        Assert.AreSame(bytes, engine.Data);
        part.SetData((byte[]?)null);

        Assert.IsNull(engine.Data);
    }

    [TestMethod]
    public void SetData_Text_PassesItsUtf8BytesAndNullPassesNull()
    {
        var (part, engine) = NewPart();

        part.SetData("hé");
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hé"), engine.Data);
        part.SetData((string?)null);

        Assert.IsNull(engine.Data);
    }

    [TestMethod]
    public void SetDataCallback_PassesTheSizeAndBothCallbacks()
    {
        var (part, engine) = NewPart();
        ReadCallback read = _ => 0;
        SeekCallback seek = (_, _) => SeekResult.Ok;

        Assert.AreEqual(CurlCode.Ok, part.SetDataCallback(-1, read, seek));

        Assert.AreEqual((-1L, read, seek), engine.Callback);
    }

    [TestMethod]
    public void SetSubParts_PassesTheNestedHandlesBuilderAndNullPassesNull()
    {
        var (part, engine) = NewPart();
        var nestedBuilder = new FakeMimeBuilder();
        using var nested = new CurlMime(nestedBuilder);

        part.SetSubParts(nested);
        Assert.AreSame(nestedBuilder, engine.SubParts);
        part.SetSubParts(null);

        Assert.IsNull(engine.SubParts);
    }

    [TestMethod]
    public void SetSubParts_AFreedNestedHandle_ThrowsObjectDisposedWithoutCallingTheEngine()
    {
        var (part, engine) = NewPart();
        var nested = new CurlMime(new FakeMimeBuilder());
        nested.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => part.SetSubParts(nested));

        Assert.IsEmpty(engine.Calls);
    }

    [TestMethod]
    public void SetHeaders_PassesACopyAndNullPassesNull()
    {
        var (part, engine) = NewPart();
        var headers = new List<string> { "X-A: 1" };

        part.SetHeaders(headers);
        headers.Add("X-B: 2");
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, engine.Headers!.ToArray());
        part.SetHeaders(null);

        Assert.IsNull(engine.Headers);
    }

    [TestMethod]
    public void Setters_AfterTheHandleIsFreed_ThrowObjectDisposedWithoutCallingTheEngine()
    {
        var builder = new FakeMimeBuilder();
        var mime = new CurlMime(builder);
        var part = mime.AddPart();
        mime.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => part.SetName("a"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => part.SetHeaders(null));

        Assert.IsEmpty(builder.Parts.Single().Calls);
    }

    private static (CurlMimePart Part, FakeMimePartBuilder Engine) NewPart()
    {
        var builder = new FakeMimeBuilder();
        var part = new CurlMime(builder).AddPart();
        return (part, builder.Parts.Single());
    }
}
