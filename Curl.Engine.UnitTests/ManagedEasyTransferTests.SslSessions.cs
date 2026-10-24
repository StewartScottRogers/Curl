using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Tls;

namespace Curl.Engine;

// curl_easy_ssls_import and curl_easy_ssls_export through a handle (BL-2786): over the share handle's cache while it
// shares CURL_LOCK_DATA_SSL_SESSION, else the engine's, and CURLE_NOT_BUILT_IN in the Schannel build Curl matches on Windows.
public sealed partial class ManagedEasyTransferTests
{
    private const string SslSessionPeer = "example.test:443";

    // One vtls_spack session: version 1, a one-byte ticket, TLS 1.3, valid until 2100-01-01.
    private static readonly byte[] PackedSslSession =
        [0x01, 0x04, 0x00, 0x01, 0xAA, 0x02, 0x03, 0x04, 0x03, 0x00, 0x00, 0x00, 0x00, 0xF4, 0x86, 0x57, 0x00];

    [TestMethod]
    public async Task ImportTlsSession_NoShare_HandlesOwnCacheHoldsItForTheHandleToExport()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        ManagedEasyTransfer importer = SessionExporting(engine);

        CurlCode imported = importer.ImportTlsSession(SslSessionPeer, null, PackedSslSession);
        List<ExportedTlsSession> exported = [];
        CurlCode code = importer.ExportTlsSessions(session => { exported.Add(session); return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.Ok, imported);
        Assert.AreEqual(CurlCode.Ok, code);
        ExportedTlsSession only = exported.Single();
        Assert.AreEqual(SslSessionPeer, only.SessionKey);
        CollectionAssert.AreEqual(PackedSslSession, only.PackedSession);
        Assert.HasCount(64, only.SaltAndHmac);
        Assert.AreEqual(4102444800L, only.ValidUntil);
        Assert.AreEqual(0x0304, only.IetfTlsId);
        Assert.IsNull(only.Alpn);
        Assert.AreEqual(0u, only.MaxEarlyData);
        await importer.DisposeAsync();
    }

    [TestMethod]
    public async Task ImportTlsSession_ShareSharesSslSessions_GoesToTheShareUnderItsLockAndNotTheEngine()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.SslSession);
        List<string> calls = [];
        share.SetLockCallback((_, data, _) => calls.Add($"lock {data}"));
        share.SetUnlockCallback((_, data) => calls.Add($"unlock {data}"));
        ManagedEasyTransfer attached = SessionExporting(engine);
        attached.Options = attached.Options with { Behaviour = attached.Options.Behaviour with { Share = share } };
        ManagedEasyTransfer unattached = SessionExporting(engine);
        calls.Clear();

        CurlCode imported = attached.ImportTlsSession(SslSessionPeer, null, PackedSslSession);
        int sharedCount = 0;
        CurlCode sharedCode = attached.ExportTlsSessions(_ => { sharedCount++; return CurlCode.Ok; });
        int engineCount = 0;
        CurlCode engineCode = unattached.ExportTlsSessions(_ => { engineCount++; return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.Ok, imported);
        Assert.AreEqual(CurlCode.Ok, sharedCode);
        Assert.AreEqual(CurlCode.Ok, engineCode);
        Assert.AreEqual(1, sharedCount);
        Assert.AreEqual(0, engineCount);
        CollectionAssert.AreEqual(new[] { "lock SslSession", "unlock SslSession", "lock SslSession", "unlock SslSession" }, calls);
        await attached.DisposeAsync();
        await unattached.DisposeAsync();
    }

    [TestMethod]
    public async Task ImportTlsSession_ShareNotSharingSslSessions_GoesToTheHandlesOwnCacheAndNotAnotherHandles()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        IShareHandle share = engine.CreateShare();
        share.Share(CurlLockData.Cookie);
        ManagedEasyTransfer attached = SessionExporting(engine);
        attached.Options = attached.Options with { Behaviour = attached.Options.Behaviour with { Share = share } };
        ManagedEasyTransfer unattached = SessionExporting(engine);

        CurlCode imported = attached.ImportTlsSession(SslSessionPeer, null, PackedSslSession);
        int ownCount = 0;
        CurlCode ownCode = attached.ExportTlsSessions(_ => { ownCount++; return CurlCode.Ok; });
        int otherCount = 0;
        CurlCode otherCode = unattached.ExportTlsSessions(_ => { otherCount++; return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.Ok, imported);
        Assert.AreEqual(CurlCode.Ok, ownCode);
        Assert.AreEqual(CurlCode.Ok, otherCode);
        Assert.AreEqual(1, ownCount);
        Assert.AreEqual(0, otherCount);
        await attached.DisposeAsync();
        await unattached.DisposeAsync();
    }

    [TestMethod]
    public async Task ImportTlsSession_RefusedPackets_AnswerReadErrorAndBadFunctionArgumentAndHoldNothing()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        ManagedEasyTransfer easy = SessionExporting(engine);

        CurlCode unpackable = easy.ImportTlsSession(SslSessionPeer, null, [0x02]);
        CurlCode noPeer = easy.ImportTlsSession(null, new byte[63], PackedSslSession);
        int count = 0;
        CurlCode code = easy.ExportTlsSessions(_ => { count++; return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.ReadError, unpackable);
        Assert.AreEqual(CurlCode.BadFunctionArgument, noPeer);
        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(0, count);
        Assert.AreEqual("packedSession", Assert.ThrowsExactly<ArgumentNullException>(() => easy.ImportTlsSession(SslSessionPeer, null, null!)).ParamName);
        Assert.AreEqual("export", Assert.ThrowsExactly<ArgumentNullException>(() => easy.ExportTlsSessions(null!)).ParamName);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task ExportTlsSessions_CallbackAnswersOtherThanOk_StopsTheExportWithThatCode()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        ManagedEasyTransfer easy = SessionExporting(engine);
        easy.ImportTlsSession("a.test:443", null, PackedSslSession);
        easy.ImportTlsSession("b.test:443", null, PackedSslSession);
        List<string?> keys = [];

        CurlCode code = easy.ExportTlsSessions(session => { keys.Add(session.SessionKey); return CurlCode.AbortedByCallback; });

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        CollectionAssert.AreEqual(new[] { "a.test:443" }, keys);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task ImportTlsSession_BuildWithoutSslsExport_AnswersNotBuiltInAndExportCallsNothing()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.ExportsTlsSessions = false;
        int count = 0;

        CurlCode imported = easy.ImportTlsSession(SslSessionPeer, null, PackedSslSession);
        CurlCode exported = easy.ExportTlsSessions(_ => { count++; return CurlCode.Ok; });
        easy.ExportsTlsSessions = true;
        CurlCode afterwards = easy.ExportTlsSessions(_ => { count++; return CurlCode.Ok; });

        Assert.AreEqual(CurlCode.NotBuiltIn, imported);
        Assert.AreEqual(CurlCode.NotBuiltIn, exported);
        Assert.AreEqual(CurlCode.Ok, afterwards);
        Assert.AreEqual(0, count);
        await easy.DisposeAsync();
    }

    [TestMethod]
    public async Task ExportsTlsSessions_NewHandle_IsTheOpenSslBuildsOffWindowsOnly()
    {
        await using ManagedCurlEngine engine = CreateEngine(new ScriptedConnector([]));
        await using IEasyTransfer easy = engine.CreateEasy();

        bool exports = ((ManagedEasyTransfer)easy).ExportsTlsSessions;

        Assert.AreEqual(!OperatingSystem.IsWindows(), exports);
    }

    private static ManagedEasyTransfer SessionExporting(ManagedCurlEngine engine)
    {
        ManagedEasyTransfer easy = (ManagedEasyTransfer)engine.CreateEasy();
        easy.ExportsTlsSessions = true;
        return easy;
    }
}
