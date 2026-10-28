using Curl.Api.Options;
using Curl.Api.Options.Protocols;

namespace Curl.Engine;

// CURLOPT_UPLOAD_FLAGS as the IMAP APPEND flag list (BL-3905); the APPEND lines are libcurl 8.22.0's, measured against a loopback IMAP server.
[TestClass]
public sealed class ManagedEasyTransferUploadFlagsTests
{
    private static string[] NamesFor(byte uploadFlags) =>
        [.. ManagedEasyTransfer.TransferSettingsOf(new EasyOptions { Protocols = new EasyProtocolOptions { Smtp = new EasySmtpOptions { UploadFlags = uploadFlags } } }).UploadFlags];

    [TestMethod]
    public void TransferSettingsOf_DefaultOptions_NamesSeen() =>
        CollectionAssert.AreEqual(new[] { "seen" }, ManagedEasyTransfer.TransferSettingsOf(new EasyOptions()).UploadFlags.ToArray());

    [TestMethod]
    public void TransferSettingsOf_NoBitsSet_NamesNothing() =>
        CollectionAssert.AreEqual(Array.Empty<string>(), NamesFor(0));

    [TestMethod]
    public void TransferSettingsOf_AllFiveBits_NamesThemInCurlsFixedOrder() =>
        CollectionAssert.AreEqual(new[] { "answered", "deleted", "draft", "flagged", "seen" }, NamesFor(31));

    [TestMethod]
    public void TransferSettingsOf_DraftAndAnsweredBits_NamesAnsweredBeforeDraft() =>
        CollectionAssert.AreEqual(new[] { "answered", "draft" }, NamesFor(5));

    [TestMethod]
    public void TransferSettingsOf_FlaggedBitAlone_NamesFlagged() =>
        CollectionAssert.AreEqual(new[] { "flagged" }, NamesFor(8));

    [TestMethod]
    public void TransferSettingsOf_BitsAboveTheFiveFlags_AreIgnored() =>
        CollectionAssert.AreEqual(new[] { "deleted" }, NamesFor(0xE2));
}
