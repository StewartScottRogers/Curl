using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Protocols;
using Curl.Original.Protocol.Rtsp;

namespace Curl.Engine;

[TestClass]
public sealed class HandleRtspSessionTests
{
    private static EasyOptions With(EasyRtspOptions rtsp) =>
        new() { Protocols = new EasyProtocolOptions { Rtsp = rtsp } };

    [TestMethod]
    public void SettingsFor_EveryRequestKind_MapsToTheHandlersKindOfTheSameName()
    {
        foreach (RtspRequest request in Enum.GetValues<RtspRequest>().Where(r => r != RtspRequest.None))
        {
            RtspRequestSettings settings = new HandleRtspSession().SettingsFor(With(new EasyRtspOptions { Request = request }));

            Assert.AreEqual(request.ToString(), settings.Kind.ToString());
        }
    }

    [TestMethod]
    public void SettingsFor_RequestNone_SendsOptions()
    {
        RtspRequestSettings settings = new HandleRtspSession().SettingsFor(With(new EasyRtspOptions { Request = RtspRequest.None }));

        Assert.AreEqual(RtspRequestKind.Options, settings.Kind);
    }

    [TestMethod]
    public void SettingsFor_StreamUriAndTransport_AreCarried()
    {
        RtspRequestSettings settings = new HandleRtspSession().SettingsFor(With(new EasyRtspOptions { StreamUri = "rtsp://h/s", Transport = "RTP/AVP" }));

        Assert.AreEqual("rtsp://h/s", settings.StreamUri);
        Assert.AreEqual("RTP/AVP", settings.Transport);
    }

    [TestMethod]
    public void SettingsFor_ClientCSeqSet_IsHandedOnOnceUntilItChanges()
    {
        HandleRtspSession session = new();
        EasyOptions options = With(new EasyRtspOptions { ClientCSeq = 999 });

        Assert.AreEqual(999L, session.SettingsFor(options).ClientSequenceNumber);
        Assert.IsNull(session.SettingsFor(options).ClientSequenceNumber);
        Assert.IsNull(new HandleRtspSession().SettingsFor(With(new EasyRtspOptions())).ClientSequenceNumber);
    }

    [TestMethod]
    public void SettingsFor_SessionIdSet_IsHandedOnOnceUntilItChanges()
    {
        HandleRtspSession session = new();
        EasyOptions options = With(new EasyRtspOptions { SessionId = "abc" });

        Assert.AreEqual("abc", session.SettingsFor(options).SessionId);
        Assert.IsNull(session.SettingsFor(options).SessionId);
    }

    [TestMethod]
    public void SettingsFor_SessionIdSetBackToNull_HandsOnNoIdAndAnUnchangedOneAgainAfterIt()
    {
        HandleRtspSession session = new();
        session.SettingsFor(With(new EasyRtspOptions { SessionId = "abc" }));

        Assert.IsNull(session.SettingsFor(With(new EasyRtspOptions())).SessionId);
        Assert.AreEqual("abc", session.SettingsFor(With(new EasyRtspOptions { SessionId = "abc" })).SessionId);
    }

    [TestMethod]
    public void SettingsFor_SameSessionIdSetAgain_IsHandedOnAgain()
    {
        HandleRtspSession session = new();
        session.SettingsFor(With(new EasyRtspOptions { SessionId = "abc", SessionIdSetCount = 1 }));

        Assert.AreEqual("abc", session.SettingsFor(With(new EasyRtspOptions { SessionId = "abc", SessionIdSetCount = 2 })).SessionId);
    }

    [TestMethod]
    public void SettingsFor_NullSessionIdSetAgain_HandsOnNoId()
    {
        HandleRtspSession session = new();
        session.SettingsFor(With(new EasyRtspOptions { SessionIdSetCount = 1 }));

        Assert.IsNull(session.SettingsFor(With(new EasyRtspOptions { SessionIdSetCount = 2 })).SessionId);
    }

    [TestMethod]
    public void SettingsFor_PostFields_AreTheBodyCutToPostFieldSize()
    {
        EasyOptions options = With(new EasyRtspOptions()) with { Http = new EasyHttpOptions { PostFields = new byte[] { 1, 2, 3 }, PostFieldSize = 2 } };

        CollectionAssert.AreEqual(new byte[] { 1, 2 }, new HandleRtspSession().SettingsFor(options).Body);
    }

    [TestMethod]
    public void SettingsFor_PostFieldsWithNoSize_AreTheWholeBody()
    {
        EasyOptions options = With(new EasyRtspOptions()) with { Http = new EasyHttpOptions { PostFields = new byte[] { 1, 2, 3 } } };

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, new HandleRtspSession().SettingsFor(options).Body);
    }

    [TestMethod]
    public void SettingsFor_Upload_LeavesTheBodyToTheHandlersUploadRead()
    {
        EasyOptions options = With(new EasyRtspOptions()) with
        {
            Http = new EasyHttpOptions { PostFields = new byte[] { 1 } },
            Transfer = new EasyTransferOptions { Upload = true },
        };

        Assert.IsNull(new HandleRtspSession().SettingsFor(options).Body);
    }

    [TestMethod]
    public void SettingsFor_NoPostFields_SendsNoBody()
    {
        Assert.IsNull(new HandleRtspSession().SettingsFor(With(new EasyRtspOptions())).Body);
    }
}
