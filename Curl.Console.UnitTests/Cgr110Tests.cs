using Curl.Api.Callbacks;
using Curl.Api.Options.Http;
using Curl.Core;
using Curl.Engine;

namespace Curl.Console;

/// <summary>
/// CGR-110 part 3 (BL-3974): with standard input closed, curl 8.22.0's <c>-F f=@-</c> sends the request head and the
/// first part's head, then its <c>fread</c> on <c>stdin</c> fails and the transfer ends with exit 42
/// (<c>operation aborted by callback</c>); an open but empty standard input reads as end of file (ADR-0967).
/// </summary>
[TestClass]
public sealed class Cgr110Tests
{
    [TestMethod]
    public void BodyOf_StandardInputPartWithStandardInputClosed_ReadCallbackAborts()
    {
        using ClosedStandardInputStream standardInput = new();
        byte[] buffer = new byte[8];

        EasyMimeCallback data = ReadCallbackOfStandardInputPart(standardInput);

        Assert.AreEqual(ReadCallbackResult.Abort, data.Read(buffer));
    }

    [TestMethod]
    public void BodyOf_StandardInputPartWithStandardInputEmpty_ReadCallbackReadsEndOfFile()
    {
        using MemoryStream standardInput = new();
        byte[] buffer = new byte[8];

        EasyMimeCallback data = ReadCallbackOfStandardInputPart(standardInput);

        Assert.AreEqual(0, data.Read(buffer));
    }

    private static EasyMimeCallback ReadCallbackOfStandardInputPart(Stream standardInput)
    {
        EasyHttpOptions http = new() { HttpPost = new() { Parts = [new EasyMimePart { Name = "f", FileName = "-", Data = new EasyMimeStandardInput() }] } };

        return (EasyMimeCallback)MimePostForm.BodyOf(http, "http", null, standardInput)!.Parts[0].Data!;
    }
}
