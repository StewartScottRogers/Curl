namespace Curl.Extended.Protocol.GoogleCloudStorage.Http;

/// <summary>Pins <see cref="GcsHttpResponseHead.ParseField"/> against RFC 9112 §5.</summary>
[TestClass]
public sealed class GcsHttpResponseHeadTests
{
    [TestMethod]
    [DataRow(" x: v", DisplayName = "leading space before a name and colon")]
    [DataRow("\tx: v", DisplayName = "leading tab before a name and colon")]
    [DataRow("x y: v", DisplayName = "space inside the name")]
    public void ParseField_WhitespaceInTheName_FailsMalformedHeaderField(string line) =>
        Assert.AreEqual(GcsHttpFailure.MalformedHeaderField, Assert.ThrowsExactly<GcsHttpException>(() => GcsHttpResponseHead.ParseField(line)).Failure);

    [TestMethod]
    public void ParseField_NameColonPaddedValue_TrimsTheValue()
    {
        var field = GcsHttpResponseHead.ParseField("Name: \t value \t");

        Assert.AreEqual("Name", field.Key);
        Assert.AreEqual("value", field.Value);
    }
}
