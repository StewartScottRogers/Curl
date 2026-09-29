namespace Curl.Http3;

/// <summary>
/// Pins <see cref="Http3Exception" />: its error code and message.
/// </summary>
[TestClass]
public sealed class Http3ExceptionTests
{
    [TestMethod]
    public void Constructor_CarriesTheErrorCodeAndReason()
    {
        Http3Exception failure = new(Http3ErrorCode.MissingSettings, "the control stream starts with a Data frame");

        Assert.AreEqual(Http3ErrorCode.MissingSettings, failure.ErrorCode);
        Assert.AreEqual("HTTP/3 MissingSettings: the control stream starts with a Data frame.", failure.Message);
    }
}
