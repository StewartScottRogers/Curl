namespace Curl.Api.Options.Behaviour;

[TestClass]
public sealed class EasyTransferOptionsTests
{
    private static readonly EasyTransferOptions Defaults = new();

    [TestMethod]
    public void New_EasyTransferOptions_LeavesRangeAndCustomRequestUnset()
    {
        Assert.IsNull(Defaults.Range);
        Assert.IsNull(Defaults.CustomRequest);
    }

    [TestMethod]
    public void New_EasyTransferOptions_LeavesEverySwitchOff()
    {
        Assert.IsFalse(Defaults.FileTime);
        Assert.IsFalse(Defaults.DirListOnly);
        Assert.IsFalse(Defaults.NoBody);
        Assert.IsFalse(Defaults.Upload);
        Assert.IsFalse(Defaults.TransferText);
        Assert.IsFalse(Defaults.Crlf);
        Assert.IsFalse(Defaults.ProxyTransferMode);
    }

    [TestMethod]
    public void New_EasyTransferOptions_StartsAtZeroWithAnUnknownUploadSize()
    {
        Assert.AreEqual(0L, Defaults.ResumeFrom);
        Assert.AreEqual(-1L, Defaults.InFileSize);
    }

    [TestMethod]
    public void New_EasyTransferOptions_UsesLibcurlsBufferSizes()
    {
        Assert.AreEqual(65536L, Defaults.UploadBufferSize);
        Assert.AreEqual(16384L, Defaults.BufferSize);
    }

    [TestMethod]
    public void New_EasyTransferOptions_SetsNoSizeSpeedOrTimeLimit()
    {
        Assert.AreEqual(0L, Defaults.MaxFileSize);
        Assert.AreEqual(0L, Defaults.LowSpeedLimit);
        Assert.AreEqual(TimeSpan.Zero, Defaults.LowSpeedTime);
        Assert.AreEqual(0L, Defaults.MaxSendSpeed);
        Assert.AreEqual(0L, Defaults.MaxReceiveSpeed);
        Assert.AreEqual(TimeSpan.Zero, Defaults.Timeout);
    }

    [TestMethod]
    public void New_EasyTransferOptions_SetsNoTimeConditionAtTheUnixEpoch()
    {
        Assert.AreEqual(TimeCondition.None, Defaults.TimeCondition);
        Assert.AreEqual(DateTimeOffset.UnixEpoch, Defaults.TimeValue);
    }

    [TestMethod]
    public void TimeCondition_Members_HoldLibcurlsTimecondValues()
    {
        Assert.AreEqual(0, (int)TimeCondition.None);
        Assert.AreEqual(1, (int)TimeCondition.IfModifiedSince);
        Assert.AreEqual(2, (int)TimeCondition.IfUnmodifiedSince);
        Assert.AreEqual(3, (int)TimeCondition.LastModified);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        AssertChangesOnly(Defaults with { Range = "0-99" }, o => o with { Range = null });
        AssertChangesOnly(Defaults with { ResumeFrom = 100 }, o => o with { ResumeFrom = 0 });
        AssertChangesOnly(Defaults with { CustomRequest = "DELETE" }, o => o with { CustomRequest = null });
        AssertChangesOnly(Defaults with { FileTime = true }, o => o with { FileTime = false });
        AssertChangesOnly(Defaults with { DirListOnly = true }, o => o with { DirListOnly = false });
        AssertChangesOnly(Defaults with { NoBody = true }, o => o with { NoBody = false });
        AssertChangesOnly(Defaults with { Upload = true }, o => o with { Upload = false });
        AssertChangesOnly(Defaults with { InFileSize = 10 }, o => o with { InFileSize = -1 });
        AssertChangesOnly(Defaults with { UploadBufferSize = 16384 }, o => o with { UploadBufferSize = 65536 });
        AssertChangesOnly(Defaults with { BufferSize = 1024 }, o => o with { BufferSize = 16384 });
        AssertChangesOnly(Defaults with { MaxFileSize = 1000 }, o => o with { MaxFileSize = 0 });
        AssertChangesOnly(Defaults with { TimeCondition = TimeCondition.IfModifiedSince }, o => o with { TimeCondition = TimeCondition.None });
        AssertChangesOnly(Defaults with { TimeValue = DateTimeOffset.UnixEpoch.AddDays(1) }, o => o with { TimeValue = DateTimeOffset.UnixEpoch });
        AssertChangesOnly(Defaults with { TransferText = true }, o => o with { TransferText = false });
        AssertChangesOnly(Defaults with { Crlf = true }, o => o with { Crlf = false });
        AssertChangesOnly(Defaults with { ProxyTransferMode = true }, o => o with { ProxyTransferMode = false });
        AssertChangesOnly(Defaults with { LowSpeedLimit = 10 }, o => o with { LowSpeedLimit = 0 });
        AssertChangesOnly(Defaults with { LowSpeedTime = TimeSpan.FromSeconds(30) }, o => o with { LowSpeedTime = TimeSpan.Zero });
        AssertChangesOnly(Defaults with { MaxSendSpeed = 1000 }, o => o with { MaxSendSpeed = 0 });
        AssertChangesOnly(Defaults with { MaxReceiveSpeed = 1000 }, o => o with { MaxReceiveSpeed = 0 });
        AssertChangesOnly(Defaults with { Timeout = TimeSpan.FromSeconds(5) }, o => o with { Timeout = TimeSpan.Zero });
    }

    private static void AssertChangesOnly(EasyTransferOptions changed, Func<EasyTransferOptions, EasyTransferOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
