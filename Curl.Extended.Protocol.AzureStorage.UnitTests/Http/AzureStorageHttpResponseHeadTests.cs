namespace Curl.Extended.Protocol.AzureStorage.Http;

/// <summary>Pins the response head parser against RFC 9112.</summary>
[TestClass]
public sealed class AzureStorageHttpResponseHeadTests
{
    [TestMethod]
    [DataRow(" X: y", DisplayName = "§5.1 space before the field-name")]
    [DataRow("\tX: y", DisplayName = "§5.1 tab before the field-name")]
    public void ParseField_WhitespaceBeforeTheName_FailsMalformedHeaderField(string line)
    {
        // RFC 9112 §5: field-name is a token, which holds no whitespace, at its first character as anywhere else.
        var exception = Assert.ThrowsExactly<AzureStorageHttpException>(() => AzureStorageHttpResponseHead.ParseField(line));

        Assert.AreEqual(AzureStorageHttpFailure.MalformedHeaderField, exception.Failure);
    }
}
