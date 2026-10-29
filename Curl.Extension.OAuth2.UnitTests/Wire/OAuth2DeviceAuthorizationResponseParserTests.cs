namespace Curl.Extension.OAuth2.Wire;

/// <summary>Pins the reading of the device authorization response against RFC 8628 §3.2.</summary>
[TestClass]
public sealed class OAuth2DeviceAuthorizationResponseParserTests
{
    [TestMethod]
    public void Parse_Rfc8628Section32Example_ReadsEveryMember()
    {
        const string Body = """
            {
              "device_code": "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS",
              "user_code": "WDJB-MJHT",
              "verification_uri": "https://example.com/device",
              "verification_uri_complete": "https://example.com/device?user_code=WDJB-MJHT",
              "expires_in": 1800,
              "interval": 7
            }
            """;

        var result = OAuth2DeviceAuthorizationResponseParser.Parse(Body);

        Assert.AreEqual(
            new OAuth2DeviceAuthorization(
                "GmRhmhcxhwAzkoEqiMEg_DnyEysNkuNhszIySk9eS",
                "WDJB-MJHT",
                "https://example.com/device",
                "https://example.com/device?user_code=WDJB-MJHT",
                1800,
                7),
            result.Value);
    }

    [TestMethod]
    public void Parse_NoInterval_DefaultsToFiveSeconds()
    {
        var result = OAuth2DeviceAuthorizationResponseParser.Parse(
            """{"device_code":"d","user_code":"u","verification_uri":"https://example.com/device","expires_in":1800}""");

        Assert.AreEqual(new OAuth2DeviceAuthorization("d", "u", "https://example.com/device", null, 1800, 5), result.Value);
    }

    [TestMethod]
    public void Parse_NoExpiresIn_FailsWithTheReason()
    {
        var result = OAuth2DeviceAuthorizationResponseParser.Parse(
            """{"device_code":"d","user_code":"u","verification_uri":"https://example.com/device"}""");

        Assert.AreEqual("The body has no \"expires_in\".", result.Failure);
    }
}
