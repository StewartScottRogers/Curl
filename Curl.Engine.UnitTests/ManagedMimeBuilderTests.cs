using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedMimeBuilderTests
{
    [TestMethod]
    public void Build_PartsWithEverySetter_GivesThePartsInAddOrderAsSet()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder first = mime.AddPart();
        IMimePartBuilder second = mime.AddPart();

        Assert.AreEqual(CurlCode.Ok, first.SetName("a"));
        Assert.AreEqual(CurlCode.Ok, first.SetFileName("r.txt"));
        Assert.AreEqual(CurlCode.Ok, first.SetType("text/x"));
        Assert.AreEqual(CurlCode.Ok, first.SetEncoder("base64"));
        Assert.AreEqual(CurlCode.Ok, first.SetHeaders(["X-A: 1"]));
        Assert.AreEqual(CurlCode.Ok, first.SetData([1, 2]));
        Assert.AreEqual(CurlCode.Ok, second.SetName("b"));
        EasyMimeBody body = mime.Build();

        Assert.HasCount(2, body.Parts);
        EasyMimePart part = body.Parts[0];
        Assert.AreEqual("a", part.Name);
        Assert.AreEqual("r.txt", part.FileName);
        Assert.AreEqual("text/x", part.Type);
        Assert.AreEqual("base64", part.Encoder);
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, part.Headers.ToArray());
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, ((EasyMimeBytes)part.Data!).Bytes.ToArray());
        Assert.IsNull(part.SubParts);
        Assert.AreEqual("b", body.Parts[1].Name);
        Assert.IsNull(body.Parts[1].Data);
    }

    [TestMethod]
    public void SetData_ArrayChangedAfterwards_KeepsTheBytesAsTheyWere()
    {
        ManagedMimeBuilder mime = new();
        byte[] data = [7];
        mime.AddPart().SetData(data);

        data[0] = 8;

        CollectionAssert.AreEqual(new byte[] { 7 }, ((EasyMimeBytes)mime.Build().Parts[0].Data!).Bytes.ToArray());
    }

    [TestMethod]
    public void SetHeaders_ListChangedAfterwardsThenNull_KeepsACopyThenClearsThem()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        List<string> headers = ["X-A: 1"];

        part.SetHeaders(headers);
        headers.Add("X-B: 2");

        Assert.HasCount(1, mime.Build().Parts[0].Headers);
        Assert.AreEqual(CurlCode.Ok, part.SetHeaders(null));
        Assert.IsEmpty(mime.Build().Parts[0].Headers);
    }

    [TestMethod]
    public void SetData_Null_ClearsTheData()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetData([1]);

        Assert.AreEqual(CurlCode.Ok, part.SetData(null));

        Assert.IsNull(mime.Build().Parts[0].Data);
    }

    [TestMethod]
    [DataRow("binary")]
    [DataRow("8bit")]
    [DataRow("7bit")]
    [DataRow("BASE64")]
    [DataRow("quoted-printable")]
    public void SetEncoder_KnownNameInAnyCase_KeepsItAsGiven(string encoder)
    {
        ManagedMimeBuilder mime = new();

        Assert.AreEqual(CurlCode.Ok, mime.AddPart().SetEncoder(encoder));

        Assert.AreEqual(encoder, mime.Build().Parts[0].Encoder);
    }

    [TestMethod]
    public void SetEncoder_UnknownName_AnswersBadFunctionArgumentAndKeepsTheEncoderBefore()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetEncoder("base64");

        Assert.AreEqual(CurlCode.BadFunctionArgument, part.SetEncoder("rot13"));

        Assert.AreEqual("base64", mime.Build().Parts[0].Encoder);
    }

    [TestMethod]
    public void SetEncoder_Null_ClearsTheEncoder()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetEncoder("7bit");

        Assert.AreEqual(CurlCode.Ok, part.SetEncoder(null));

        Assert.IsNull(mime.Build().Parts[0].Encoder);
    }

    [TestMethod]
    public void SetFileData_ExistingFile_SetsTheFileAndItsBaseNameAsTheFileName()
    {
        string path = Path.GetTempFileName();
        try
        {
            ManagedMimeBuilder mime = new();

            Assert.AreEqual(CurlCode.Ok, mime.AddPart().SetFileData(path));

            EasyMimePart part = mime.Build().Parts[0];
            Assert.AreEqual(path, ((EasyMimeFile)part.Data!).Path);
            Assert.AreEqual(Path.GetFileName(path), part.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void SetFileData_Directory_AnswersOk()
    {
        ManagedMimeBuilder mime = new();

        Assert.AreEqual(CurlCode.Ok, mime.AddPart().SetFileData(Path.GetTempPath()));
    }

    [TestMethod]
    public void SetFileData_MissingFile_AnswersReadErrorAndStillSetsTheFile()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt");
        ManagedMimeBuilder mime = new();

        Assert.AreEqual(CurlCode.ReadError, mime.AddPart().SetFileData(path));

        EasyMimePart part = mime.Build().Parts[0];
        Assert.AreEqual(path, ((EasyMimeFile)part.Data!).Path);
        Assert.AreEqual("missing.txt", part.FileName);
    }

    [TestMethod]
    public void SetFileData_Null_ClearsTheDataAndKeepsTheFileName()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetFileName("keep.txt");
        part.SetData([1]);

        Assert.AreEqual(CurlCode.Ok, part.SetFileData(null));

        Assert.IsNull(mime.Build().Parts[0].Data);
        Assert.AreEqual("keep.txt", mime.Build().Parts[0].FileName);
    }

    [TestMethod]
    public void SetDataCallback_ReadCallback_GivesACallbackPartWithItsSizeAndDelegates()
    {
        ManagedMimeBuilder mime = new();
        ReadCallback read = _ => 0;
        SeekCallback seek = (_, _) => SeekResult.Ok;

        Assert.AreEqual(CurlCode.Ok, mime.AddPart().SetDataCallback(5, read, seek));

        EasyMimeCallback callback = (EasyMimeCallback)mime.Build().Parts[0].Data!;
        Assert.AreEqual(5, callback.Size);
        Assert.AreSame(read, callback.Read);
        Assert.AreSame(seek, callback.Seek);
    }

    [TestMethod]
    public void SetDataCallback_NullReadCallback_ClearsTheData()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetData([1]);

        Assert.AreEqual(CurlCode.Ok, part.SetDataCallback(-1, null, null));

        Assert.IsNull(mime.Build().Parts[0].Data);
    }

    [TestMethod]
    public void SetSubParts_NestedBody_IsBuiltAsThePartsSubPartsWithNoData()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder part = mime.AddPart();
        part.SetData([1]);
        ManagedMimeBuilder nested = new();
        nested.AddPart().SetName("x");

        Assert.AreEqual(CurlCode.Ok, part.SetSubParts(nested));
        Assert.AreEqual(CurlCode.Ok, part.SetSubParts(nested));

        EasyMimePart built = mime.Build().Parts[0];
        Assert.IsNull(built.Data);
        Assert.AreEqual("x", built.SubParts!.Parts[0].Name);
    }

    [TestMethod]
    public void SetSubParts_BodyOfAnotherEngine_AnswersBadFunctionArgument()
    {
        ManagedMimeBuilder mime = new();

        Assert.AreEqual(CurlCode.BadFunctionArgument, mime.AddPart().SetSubParts(new ForeignMimeBuilder()));
    }

    [TestMethod]
    public void SetSubParts_BodyAnotherPartHolds_AnswersBadFunctionArgument()
    {
        ManagedMimeBuilder mime = new();
        ManagedMimeBuilder nested = new();
        mime.AddPart().SetSubParts(nested);

        Assert.AreEqual(CurlCode.BadFunctionArgument, mime.AddPart().SetSubParts(nested));
    }

    [TestMethod]
    public void SetSubParts_TheBodyThePartIsIn_AnswersBadFunctionArgument()
    {
        ManagedMimeBuilder mime = new();

        Assert.AreEqual(CurlCode.BadFunctionArgument, mime.AddPart().SetSubParts(mime));
    }

    [TestMethod]
    public void SetSubParts_ABodyEnclosingThePartsBody_AnswersBadFunctionArgument()
    {
        ManagedMimeBuilder outer = new();
        ManagedMimeBuilder inner = new();
        outer.AddPart().SetSubParts(inner);
        IMimePartBuilder innerPart = inner.AddPart();

        Assert.AreEqual(CurlCode.BadFunctionArgument, innerPart.SetSubParts(outer));
        Assert.AreEqual(CurlCode.Ok, innerPart.SetSubParts(new ManagedMimeBuilder()));
    }

    [TestMethod]
    public void SetSubParts_NullThenAnotherPartTakesTheBody_FreesItForTheOtherPart()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder first = mime.AddPart();
        ManagedMimeBuilder nested = new();
        first.SetSubParts(nested);

        Assert.AreEqual(CurlCode.Ok, first.SetSubParts(null));

        Assert.IsNull(mime.Build().Parts[0].SubParts);
        Assert.AreEqual(CurlCode.Ok, mime.AddPart().SetSubParts(nested));
    }

    [TestMethod]
    public void SetData_AfterSubParts_ReplacesThemAndFreesTheBody()
    {
        ManagedMimeBuilder mime = new();
        IMimePartBuilder first = mime.AddPart();
        ManagedMimeBuilder nested = new();
        first.SetSubParts(nested);

        first.SetData([3]);

        EasyMimePart built = mime.Build().Parts[0];
        Assert.IsNull(built.SubParts);
        Assert.IsInstanceOfType<EasyMimeBytes>(built.Data);
        Assert.IsNull(nested.Parent);
    }

    /// <summary>A MIME handle of some other engine, which a managed part cannot nest.</summary>
    private sealed class ForeignMimeBuilder : IMimeBuilder
    {
        public IMimePartBuilder AddPart() => throw new NotSupportedException();

        public EasyMimeBody Build() => new();
    }
}
