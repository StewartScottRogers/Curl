namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>Pins the integer fields <c>Upload-Offset</c> (§8.1), <c>Upload-Length</c> (§8.4) and <c>Upload-Draft-Interop-Version</c> against RFC 9651 §3.3.1 (sf-integer).</summary>
[TestClass]
public sealed class IetfUploadOffsetFieldTests
{
    [TestMethod]
    public void Format_NonNegative_IsDecimalDigits()
    {
        Assert.AreEqual("0", IetfUploadOffsetField.Format(0));
        Assert.AreEqual("999999999999999", IetfUploadOffsetField.Format(999_999_999_999_999));
        Assert.AreEqual("42", IetfUploadLengthField.Format(42));
        Assert.AreEqual("6", IetfUploadDraftInteropVersionField.Format(6));
    }

    [TestMethod]
    [DataRow(-1L, DisplayName = "§8.1 non-negative")]
    [DataRow(1_000_000_000_000_000L, DisplayName = "RFC 9651 §3.3.1 at most 15 digits")]
    public void Format_OutOfRange_Throws(long offset) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IetfUploadOffsetField.Format(offset));

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("123", 123L)]
    [DataRow(" 7 ", 7L, DisplayName = "surrounding SP")]
    [DataRow("5;x", 5L, DisplayName = "RFC 9651 §3.1.2 parameters are skipped")]
    public void TryParse_Integer_ReturnsIt(string value, long expected)
    {
        Assert.IsTrue(IetfUploadOffsetField.TryParse(value, out var offset));
        Assert.IsTrue(IetfUploadLengthField.TryParse(value, out var length));
        Assert.IsTrue(IetfUploadDraftInteropVersionField.TryParse(value, out var version));

        Assert.AreEqual(expected, offset);
        Assert.AreEqual(expected, length);
        Assert.AreEqual(expected, version);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "absent")]
    [DataRow("", DisplayName = "§3.3.1 at least one digit")]
    [DataRow("-1", DisplayName = "§8.1 non-negative")]
    [DataRow("+1", DisplayName = "§3.3.1 no plus sign")]
    [DataRow("1.5", DisplayName = "§3.3.2 a decimal is not an integer")]
    [DataRow("1000000000000000", DisplayName = "§3.3.1 at most 15 digits")]
    [DataRow("abc", DisplayName = "§3.3.1 digits")]
    public void TryParse_Malformed_IsRefused(string? value)
    {
        Assert.IsFalse(IetfUploadOffsetField.TryParse(value, out var offset));
        Assert.IsFalse(IetfUploadLengthField.TryParse(value, out _));
        Assert.IsFalse(IetfUploadDraftInteropVersionField.TryParse(value, out _));
        Assert.AreEqual(0, offset);
    }
}
