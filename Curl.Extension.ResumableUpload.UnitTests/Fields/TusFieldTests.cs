namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>Pins tus 1.0.0's fields, section "Headers": <c>Tus-Resumable</c>, <c>Tus-Version</c>, <c>Tus-Extension</c>, <c>Upload-Offset</c> and <c>Upload-Length</c>.</summary>
[TestClass]
public sealed class TusFieldTests
{
    [TestMethod]
    [DataRow("1.0.0", true, DisplayName = "Tus-Resumable 1.0.0")]
    [DataRow("0.2.2", true, DisplayName = "Tus-Resumable another version")]
    [DataRow("1..0", false, DisplayName = "Tus-Resumable an empty part")]
    [DataRow("1.0.x", false, DisplayName = "Tus-Resumable not digits")]
    [DataRow("", false, DisplayName = "Tus-Resumable empty")]
    [DataRow(null, false, DisplayName = "Tus-Resumable absent")]
    public void TusResumable_TryParse_AcceptsOnlyAVersion(string? value, bool expected)
    {
        Assert.AreEqual(expected, TusResumableField.TryParse(value, out var version));

        Assert.AreEqual(expected ? value : string.Empty, version);
    }

    [TestMethod]
    public void TusVersion_FormatAndParse_RoundTripAList()
    {
        Assert.AreEqual("1.0.0,0.2.2", TusVersionField.Format(["1.0.0", "0.2.2"]));
        Assert.IsTrue(TusVersionField.TryParse("1.0.0, 0.2.2", out var versions));
        CollectionAssert.AreEqual(new[] { "1.0.0", "0.2.2" }, versions.ToArray());
    }

    [TestMethod]
    public void TusExtension_FormatAndParse_RoundTripAList()
    {
        Assert.AreEqual("creation,termination", TusExtensionField.Format(["creation", "termination"]));
        Assert.IsTrue(TusExtensionField.TryParse("creation,creation-with-upload ,expiration", out var extensions));
        CollectionAssert.AreEqual(new[] { "creation", "creation-with-upload", "expiration" }, extensions.ToArray());
    }

    [TestMethod]
    [DataRow(null, DisplayName = "absent")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("creation,,termination", DisplayName = "an empty item")]
    [DataRow("creation, bad item", DisplayName = "an item that is not a token")]
    public void TusExtension_TryParse_Malformed_IsRefused(string? value)
    {
        Assert.IsFalse(TusExtensionField.TryParse(value, out var extensions));
        Assert.IsFalse(TusVersionField.TryParse(value, out _));

        Assert.IsEmpty(extensions);
    }

    [TestMethod]
    public void TusLists_FormatInvalid_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => TusVersionField.Format([]));
        Assert.ThrowsExactly<ArgumentException>(() => TusVersionField.Format(["one"]));
        Assert.ThrowsExactly<ArgumentException>(() => TusExtensionField.Format(["bad item"]));
        Assert.ThrowsExactly<ArgumentNullException>(() => TusExtensionField.Format(null!));
    }

    [TestMethod]
    public void TusIntegers_Format_AreDecimalDigits()
    {
        Assert.AreEqual("0", TusUploadOffsetField.Format(0));
        Assert.AreEqual("1234567890123456789", TusUploadLengthField.Format(1234567890123456789));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TusUploadOffsetField.Format(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => TusUploadLengthField.Format(-1));
    }

    [TestMethod]
    [DataRow("0", true, 0L)]
    [DataRow("1234567890123456789", true, 1234567890123456789L, DisplayName = "no 15-digit limit")]
    [DataRow("-1", false, 0L, DisplayName = "non-negative")]
    [DataRow("+1", false, 0L, DisplayName = "no sign")]
    [DataRow("1 ", false, 0L, DisplayName = "digits only")]
    [DataRow("", false, 0L, DisplayName = "empty")]
    [DataRow(null, false, 0L, DisplayName = "absent")]
    public void TusIntegers_TryParse_AcceptOnlyDecimalDigits(string? value, bool expected, long expectedValue)
    {
        Assert.AreEqual(expected, TusUploadOffsetField.TryParse(value, out var offset));
        Assert.AreEqual(expected, TusUploadLengthField.TryParse(value, out var length));

        Assert.AreEqual(expectedValue, offset);
        Assert.AreEqual(expectedValue, length);
    }
}
