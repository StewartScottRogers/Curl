namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>
/// Pins the <c>CompleteMultipartUpload</c> request body of the Amazon S3 API reference
/// "CompleteMultipartUpload", against its example body.
/// </summary>
[TestClass]
public sealed class S3CompleteMultipartUploadWriterTests
{
    [TestMethod]
    public void Write_ReferenceExampleCompleteMultipartUpload_ListsThePartsInAscendingOrder()
    {
        // "CompleteMultipartUpload", "Example 1: Complete multipart upload", sample request body,
        // with its parts handed over out of order.
        S3UploadedPart[] parts =
        [
            new(3, "\"acbd18db4cc2f85cedef654fccc4a4d8\""),
            new(1, "\"a54357aff0632cce46d942af68356b38\""),
            new(2, "\"0c78aef83f66abc1fa1e8477f296d394\""),
        ];

        var body = S3CompleteMultipartUploadWriter.Write(parts);

        Assert.AreEqual(
            "<CompleteMultipartUpload xmlns=\"http://s3.amazonaws.com/doc/2006-03-01/\">"
            + "<Part><PartNumber>1</PartNumber><ETag>\"a54357aff0632cce46d942af68356b38\"</ETag></Part>"
            + "<Part><PartNumber>2</PartNumber><ETag>\"0c78aef83f66abc1fa1e8477f296d394\"</ETag></Part>"
            + "<Part><PartNumber>3</PartNumber><ETag>\"acbd18db4cc2f85cedef654fccc4a4d8\"</ETag></Part>"
            + "</CompleteMultipartUpload>",
            body);
    }

    [TestMethod]
    public void Write_ETagWithMarkup_EscapesIt()
    {
        var body = S3CompleteMultipartUploadWriter.Write([new(1, "a<b&c")]);

        StringAssert.Contains(body, "<ETag>a&lt;b&amp;c</ETag>");
    }

    [TestMethod]
    public void Write_Null_Throws() => Assert.ThrowsExactly<ArgumentNullException>(() => S3CompleteMultipartUploadWriter.Write(null!));
}
