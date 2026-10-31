namespace Curl.Extension.ResumableUpload.Fields;

/// <summary>Pins <c>Upload-Limit</c> against draft-ietf-httpbis-resumable-upload §8.2 and RFC 9651 §3.2 (sf-dictionary).</summary>
[TestClass]
public sealed class IetfUploadLimitFieldTests
{
    [TestMethod]
    public void Format_EveryLimit_ListsThemInTheDraftsOrder()
    {
        var value = IetfUploadLimitField.Format(new ResumableUploadLimit(1000, 1, 500, 10, 3600));

        Assert.AreEqual("max-size=1000, min-size=1, max-append-size=500, min-append-size=10, expires=3600", value);
    }

    [TestMethod]
    public void Format_OneLimit_ListsOnlyIt() =>
        Assert.AreEqual("expires=60", IetfUploadLimitField.Format(new ResumableUploadLimit(Expires: 60)));

    [TestMethod]
    public void Format_NoLimit_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => IetfUploadLimitField.Format(new ResumableUploadLimit()));
        Assert.ThrowsExactly<ArgumentNullException>(() => IetfUploadLimitField.Format(null!));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IetfUploadLimitField.Format(new ResumableUploadLimit(MaxSize: -1)));
    }

    [TestMethod]
    public void TryParse_EveryKey_ReadsEachLimit()
    {
        Assert.IsTrue(IetfUploadLimitField.TryParse("max-size=1000,min-size=1 ,\tmax-append-size=500, min-append-size=10, expires=3600", out var limit));

        Assert.AreEqual(new ResumableUploadLimit(1000, 1, 500, 10, 3600), limit);
    }

    [TestMethod]
    [DataRow("max-size=5, future-key=\"x\", *other, expires=2", DisplayName = "§8.2 unknown keys are ignored")]
    [DataRow("max-size=9, max-size=5, expires=2", DisplayName = "RFC 9651 §3.2 the last member with a key wins")]
    [DataRow("max-size=5;p=1, expires=2", DisplayName = "RFC 9651 §3.1.2 parameters are skipped")]
    public void TryParse_DictionaryRules_KeepTheKnownLimits(string value)
    {
        Assert.IsTrue(IetfUploadLimitField.TryParse(value, out var limit));

        Assert.AreEqual(new ResumableUploadLimit(MaxSize: 5, Expires: 2), limit);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "absent")]
    [DataRow("", DisplayName = "empty")]
    [DataRow("Max-Size=5", DisplayName = "RFC 9651 §3.1.2 keys are lowercase")]
    [DataRow("max-size=-5", DisplayName = "§8.2 non-negative")]
    [DataRow("max-size=\"5\"", DisplayName = "§8.2 an sf-integer, not a string")]
    [DataRow("max-size", DisplayName = "§8.2 a bare key is a boolean, not an integer")]
    [DataRow("max-size;p=1", DisplayName = "§8.2 a boolean with parameters")]
    [DataRow("max-size=5,,expires=2", DisplayName = "RFC 9651 §3.2 an empty member")]
    public void TryParse_Malformed_IsRefused(string? value)
    {
        Assert.IsFalse(IetfUploadLimitField.TryParse(value, out var limit));

        Assert.IsNull(limit);
    }
}
