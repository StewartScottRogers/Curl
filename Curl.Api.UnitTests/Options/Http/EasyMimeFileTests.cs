namespace Curl.Api.Options.Http;

[TestClass]
public sealed class EasyMimeFileTests
{
    [TestMethod]
    public void New_EasyMimeFileWithOnlyAPath_FailsTheTransferWhenTheFileIsMissing()
    {
        EasyMimeFile file = new("missing.txt");

        Assert.AreEqual("missing.txt", file.Path);
        Assert.IsFalse(file.SendsEmptyWhenMissing);
    }

    [TestMethod]
    public void New_EasyMimeFileSendingEmptyWhenMissing_KeepsThatChoice()
    {
        EasyMimeFile file = new("missing.txt", SendsEmptyWhenMissing: true);

        Assert.IsTrue(file.SendsEmptyWhenMissing);
    }
}
