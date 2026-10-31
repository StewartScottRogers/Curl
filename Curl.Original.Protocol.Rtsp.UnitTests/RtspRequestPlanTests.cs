namespace Curl.Original.Protocol.Rtsp;

[TestClass]
public sealed class RtspRequestPlanTests
{
    [TestMethod]
    public async Task WithUploadAsync_AnnounceWithNoBody_TakesTheUploadAsTheBody()
    {
        RtspRequestPlan plan = RtspRequestPlan.For(new RtspRequestSettings { Kind = RtspRequestKind.Announce });

        RtspRequestPlan sent = await plan.WithUploadAsync(new MemoryStream("v=0\r\n"u8.ToArray()), CancellationToken.None);

        CollectionAssert.AreEqual("v=0\r\n"u8.ToArray(), sent.SentBody);
    }

    [TestMethod]
    public async Task WithUploadAsync_SettingsGaveABody_KeepsThatBody()
    {
        RtspRequestPlan plan = RtspRequestPlan.For(new RtspRequestSettings { Kind = RtspRequestKind.SetParameter, Body = [1, 2] });

        RtspRequestPlan sent = await plan.WithUploadAsync(new MemoryStream([9]), CancellationToken.None);

        Assert.AreSame(plan, sent);
    }

    [TestMethod]
    public async Task WithUploadAsync_SetupSendsNoBody_LeavesTheUploadUnread()
    {
        RtspRequestPlan plan = RtspRequestPlan.For(new RtspRequestSettings { Kind = RtspRequestKind.Setup });
        MemoryStream upload = new([9]);

        RtspRequestPlan sent = await plan.WithUploadAsync(upload, CancellationToken.None);

        Assert.AreSame(plan, sent);
        Assert.AreEqual(0, upload.Position);
    }

    [TestMethod]
    public async Task WithUploadAsync_ToolTransfer_IsUnchanged()
    {
        RtspRequestPlan sent = await RtspRequestPlan.ToolTransfer.WithUploadAsync(new MemoryStream([9]), CancellationToken.None);

        Assert.AreSame(RtspRequestPlan.ToolTransfer, sent);
    }

    [TestMethod]
    public async Task WithUploadAsync_NoUpload_IsUnchanged()
    {
        RtspRequestPlan plan = RtspRequestPlan.For(new RtspRequestSettings { Kind = RtspRequestKind.GetParameter });

        Assert.AreSame(plan, await plan.WithUploadAsync(null, CancellationToken.None));
    }
}
