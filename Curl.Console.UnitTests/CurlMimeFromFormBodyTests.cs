using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Curl.Engine;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Mime;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlMimeFromFormBody" /> (ADR-0650): the <c>-F</c> form becomes a <c>curl_mime</c> tree that, set as
/// <c>CURLOPT_MIMEPOST</c>, gives the handle the form as the tool mapped it, and a form with a part no
/// <c>curl_mime_*</c> call can build is not built at all.
/// </summary>
[TestClass]
public sealed class CurlMimeFromFormBodyTests
{
    private readonly CurlLibrary library = CurlGlobal.Init(new ManagedCurlEngine());

    [TestMethod]
    public void Of_NoForm_BuildsNone()
    {
        CurlMime? mime = CurlMimeFromFormBody.Of(library.MimeInit, form: null);

        Assert.IsNull(mime);
    }

    [TestMethod]
    public async Task Of_TextFileNestedAndEmptyParts_SetsTheFormAsTheToolMappedIt()
    {
        EasyMimeBody form = new()
        {
            Parts =
            [
                new EasyMimePart { Name = "text", FileName = "t.txt", Type = "text/plain", Encoder = "base64", Headers = ["X-A: 1"], Data = new EasyMimeBytes(Encoding.UTF8.GetBytes("hello")) },
                new EasyMimePart { Name = "content", Data = new EasyMimeFile("missing/data.bin") },
                new EasyMimePart { Name = "nested", SubParts = new EasyMimeBody { Parts = [new EasyMimePart { Name = "inner", Data = new EasyMimeBytes(new byte[] { 7 }) }] } },
                new EasyMimePart { Name = "empty" },
            ],
        };

        using CurlMime? mime = CurlMimeFromFormBody.Of(library.MimeInit, form);
        await using CurlEasy easy = library.EasyInit();
        CurlCode code = easy.SetOption(CurlOption.Mimepost, mime);
        IReadOnlyList<EasyMimePart> parts = easy.Options.Http.MimePost!.Parts;

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(new[] { "text", "content", "nested", "empty" }, parts.Select(part => part.Name).ToArray());
        Assert.AreEqual("t.txt", parts[0].FileName);
        Assert.AreEqual("text/plain", parts[0].Type);
        Assert.AreEqual("base64", parts[0].Encoder);
        CollectionAssert.AreEqual(new[] { "X-A: 1" }, parts[0].Headers.ToArray());
        Assert.AreEqual("hello", Encoding.UTF8.GetString(((EasyMimeBytes)parts[0].Data!).Bytes.Span));
        Assert.IsNull(parts[1].FileName);
        Assert.AreEqual("missing/data.bin", ((EasyMimeFile)parts[1].Data!).Path);
        Assert.AreEqual("inner", parts[2].SubParts!.Parts[0].Name);
        Assert.IsNull(parts[3].Data);
        Assert.IsNull(parts[3].SubParts);
    }

    [TestMethod]
    public void Of_StandardInputPartInANestedBody_BuildsNone()
    {
        EasyMimeBody form = new()
        {
            Parts = [new EasyMimePart { Name = "nested", SubParts = new EasyMimeBody { Parts = [new EasyMimePart { Name = "in", Data = new EasyMimeStandardInput() }] } }],
        };

        CurlMime? mime = CurlMimeFromFormBody.Of(library.MimeInit, form);

        Assert.IsNull(mime);
    }

    [TestMethod]
    public void Of_EncoderTheSetterRefuses_BuildsNone()
    {
        EasyMimeBody form = new() { Parts = [new EasyMimePart { Name = "a", Encoder = "rot13", Data = new EasyMimeBytes(new byte[] { 1 }) }] };

        CurlMime? mime = CurlMimeFromFormBody.Of(library.MimeInit, form);

        Assert.IsNull(mime);
    }
}
