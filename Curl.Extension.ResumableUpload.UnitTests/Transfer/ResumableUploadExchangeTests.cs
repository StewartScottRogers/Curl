namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>Pins which final responses a resumable upload's step takes as success (RFC 9110 §15.3).</summary>
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
        var exchange = new ResumableUploadExchange("HEAD", new Uri("http://up.example.com/"), statusCode, "Reason", [], []);

        Assert.AreEqual(expected, exchange.IsSuccess);
    }
}
