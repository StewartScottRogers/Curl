using Curl.Protocol.Abstractions;

namespace Curl.Console.UnitTests;

/// <summary>
/// CGR-002 (BL-3531): a <c>--retry</c> attempt sends the whole <c>-T</c> and <c>-F</c> body again.
/// </summary>
[TestClass]
public sealed class Cgr002Tests
{
    [TestMethod]
    public void Rewind_UploadReadToItsEnd_SeeksBackToItsStart()
    {
        using MemoryStream upload = new("xupload"u8.ToArray());
        upload.Position = 1;
        AttemptUploadRewinder rewinder = new(upload, formBody: null);
        upload.ReadExactly(new byte[6]);

        rewinder.Rewind();

        Assert.AreEqual(1, upload.Position);
    }

    [TestMethod]
    public void Rewind_FormBodyReadToItsEnd_SeeksBackToItsStart()
    {
        using MemoryStream content = new("--form--"u8.ToArray());
        AttemptUploadRewinder rewinder = new(upload: null, new StreamBody(content, 8, "multipart/form-data"));
        content.ReadExactly(new byte[8]);

        rewinder.Rewind();

        Assert.AreEqual(0, content.Position);
    }

    [TestMethod]
    public void Rewind_StreamThatCannotSeek_IsLeftWhereItIs()
    {
        using UnseekableStream upload = new();
        AttemptUploadRewinder rewinder = new(upload, new BytesBody("a=b"u8.ToArray(), "application/x-www-form-urlencoded"));

        rewinder.Rewind();

        Assert.AreEqual(0, upload.PositionSets);
    }

    private sealed class UnseekableStream : MemoryStream
    {
        public int PositionSets { get; private set; }

        public override bool CanSeek => false;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => PositionSets++;
        }
    }
}
