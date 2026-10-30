namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>Pins <c>Upload-Complete</c> against draft-ietf-httpbis-resumable-upload §8.3 and RFC 9651 §3.3.6 (sf-boolean).</summary>
[TestClass]
public sealed class IetfUploadCompleteFieldTests
{
    [TestMethod]
    public void Format_TrueAndFalse_AreQuestionMarkOneAndZero()
    {
        Assert.AreEqual("?1", IetfUploadCompleteField.Format(true));
        Assert.AreEqual("?0", IetfUploadCompleteField.Format(false));
    }

    [TestMethod]
    [DataRow("?1", true, DisplayName = "§8.3 ?1")]
    [DataRow("?0", false, DisplayName = "§8.3 ?0")]
    [DataRow("?1;a=1", true, DisplayName = "RFC 9651 §3.1.2 parameters are skipped")]
    public void TryParse_Boolean_ReturnsIt(string value, bool expected)
    {
        Assert.IsTrue(IetfUploadCompleteField.TryParse(value, out var complete));

        Assert.AreEqual(expected, complete);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "absent")]
    [DataRow("1", DisplayName = "§3.3.6 not ?0 or ?1")]
    [DataRow("?2", DisplayName = "§3.3.6 ?2")]
    [DataRow("true", DisplayName = "§3.3.6 a token")]
    [DataRow("", DisplayName = "empty")]
    public void TryParse_Malformed_IsRefused(string? value) =>
        Assert.IsFalse(IetfUploadCompleteField.TryParse(value, out _));
}
