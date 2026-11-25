namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Pins which final responses a resumable upload's step takes as success (RFC 9110 §15.3) and how it finds a field.</summary>
[TestClass]
public sealed class ResumableUploadExchangeTests
{
    [TestMethod]
    [DataRow(101, false)]
    [DataRow(200, true)]
    [DataRow(299, true)]
    [DataRow(300, false)]
    public void IsSuccess_IsTrueOnlyFor2xx(int statusCode, bool expected)
    {
        var exchange = new ResumableUploadExchange("HEAD", new Uri("http://up.example.com/"), statusCode, [], [], []);

        Assert.AreEqual(expected, exchange.IsSuccess);
    }

    [TestMethod]
    public void FindHeader_GivesTheFirstFieldOfTheNameInAnyCase()
    {
        var exchange = new ResumableUploadExchange("HEAD", new Uri("http://up.example.com/"), 200, [new("A", "1"), new("upload-offset", "2"), new("Upload-Offset", "3")], [], []);

        Assert.AreEqual("2", exchange.FindHeader("Upload-Offset"));
        Assert.IsNull(exchange.FindHeader("Location"));
    }
}
