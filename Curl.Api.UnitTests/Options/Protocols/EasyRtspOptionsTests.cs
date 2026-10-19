using Curl.Api.Options.Protocols;

namespace Curl.Api.UnitTests.Options.Protocols;

[TestClass]
public sealed class EasyRtspOptionsTests
{
    private static readonly EasyRtspOptions Defaults = new();

    [TestMethod]
    public void New_EveryOption_DefaultsToLibcurlDefault()
    {
        Assert.AreEqual(RtspRequest.Options, Defaults.Request);
        Assert.IsNull(Defaults.SessionId);
        Assert.AreEqual(0L, Defaults.SessionIdSetCount);
        Assert.AreEqual("*", Defaults.StreamUri);
        Assert.IsNull(Defaults.Transport);
        Assert.AreEqual(0L, Defaults.ClientCSeq);
        Assert.AreEqual(0L, Defaults.ServerCSeq);
    }

    [TestMethod]
    public void RtspRequest_CarriesLibcurlNumbers()
    {
        RtspRequest[] inLibcurlOrder =
        [
            RtspRequest.None, RtspRequest.Options, RtspRequest.Describe, RtspRequest.Announce,
            RtspRequest.Setup, RtspRequest.Play, RtspRequest.Pause, RtspRequest.Teardown,
            RtspRequest.GetParameter, RtspRequest.SetParameter, RtspRequest.Record, RtspRequest.Receive,
        ];

        for (int number = 0; number < inLibcurlOrder.Length; number++)
        {
            Assert.AreEqual(number, (int)inLibcurlOrder[number], inLibcurlOrder[number].ToString());
        }

        Assert.HasCount(inLibcurlOrder.Length, Enum.GetValues<RtspRequest>());
    }

    [TestMethod]
    public void With_OneOption_ChangesOnlyThatOption()
    {
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { Request = RtspRequest.Setup }, o => o with { Request = Defaults.Request });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { SessionId = "12345678" }, o => o with { SessionId = Defaults.SessionId });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { SessionIdSetCount = 1 }, o => o with { SessionIdSetCount = Defaults.SessionIdSetCount });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { StreamUri = "rtsp://example.com/media" }, o => o with { StreamUri = Defaults.StreamUri });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { Transport = "RTP/AVP;unicast" }, o => o with { Transport = Defaults.Transport });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { ClientCSeq = 5 }, o => o with { ClientCSeq = Defaults.ClientCSeq });
        OptionRecordAssert.ChangesOnly(Defaults, Defaults with { ServerCSeq = 5 }, o => o with { ServerCSeq = Defaults.ServerCSeq });
        Assert.HasCount(7, typeof(EasyRtspOptions).GetProperties());
    }
}
