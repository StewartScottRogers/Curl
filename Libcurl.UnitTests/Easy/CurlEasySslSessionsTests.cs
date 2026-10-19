using Curl.Api.Codes;
using Curl.Api.EngineContracts.Tls;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Options;

namespace Libcurl.UnitTests.Easy;

[TestClass]
public sealed class CurlEasySslSessionsTests
{
    // A version 1 packet holding a one-byte ticket, as curl 8.22.0's vtls_spack.c packs it.
    private static readonly byte[] OneByteTicket = [0x01, 0x04, 0x00, 0x01, (byte)'B'];

    // The same ticket from TLS 1.3, valid until 2100-01-01: a packet with no valid-until is already expired, so the
    // engine's cache drops it before an export, as curl's does.
    private static readonly byte[] OneByteTicketValidUntil2100 =
        [0x01, 0x04, 0x00, 0x01, (byte)'B', 0x02, 0x03, 0x04, 0x03, 0x00, 0x00, 0x00, 0x00, 0xF4, 0x86, 0x57, 0x00];

    [TestMethod]
    public void SslsImport_AKeyAndAPacket_PassesBothToTheEngineAndAnswersItsCode()
    {
        var transfer = new FakeEasyTransfer { ImportResult = CurlCode.ReadError };
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);

        CurlCode code = easy.SslsImport("example.test:443", null, OneByteTicket);

        Assert.AreEqual(CurlCode.ReadError, code);
        Assert.HasCount(1, transfer.Imported);
        Assert.AreEqual("example.test:443", transfer.Imported[0].SessionKey);
        Assert.IsNull(transfer.Imported[0].SaltAndHmac);
        CollectionAssert.AreEqual(OneByteTicket, transfer.Imported[0].PackedSession);
    }

    [TestMethod]
    public void SslsImport_NoPacket_PassesTheEngineAnEmptyOne()
    {
        var transfer = new FakeEasyTransfer();
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        byte[] shmac = new byte[64];

        CurlCode code = easy.SslsImport(null, shmac, null);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreSame(shmac, transfer.Imported[0].SaltAndHmac);
        Assert.IsEmpty(transfer.Imported[0].PackedSession);
    }

    [TestMethod]
    public void SslsImport_AfterCleanup_ThrowsObjectDisposed()
    {
        var easy = new CurlEasy(new FakeEasyTransfer(), EasyOptionDispatch.Default);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.SslsImport("example.test:443", null, OneByteTicket));
    }

    [TestMethod]
    public void SslsExport_TwoHeldSessions_HandsEachToTheCallbackWithTheHandle()
    {
        var first = new ExportedTlsSession("example.test:443", new byte[64], OneByteTicket, 1_900_000_000, 0x0304, "h2", 16384);
        var second = new ExportedTlsSession(null, new byte[64], [0x01], 1_900_000_001, 0x0303, null, 0);
        var transfer = new FakeEasyTransfer { HeldSessions = [first, second] };
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        var seen = new List<string>();

        CurlCode code = easy.SslsExport((handle, key, shmac, sdata, validUntil, tlsId, alpn, earlyData) =>
        {
            Assert.AreSame(easy, handle);
            seen.Add($"{key}|{shmac.Length}|{sdata.Length}|{validUntil}|{tlsId:x4}|{alpn}|{earlyData}");
            return CurlCode.Ok;
        });

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(
            new[] { "example.test:443|64|5|1900000000|0304|h2|16384", "|64|1|1900000001|0303||0" },
            seen);
    }

    [TestMethod]
    public void SslsExport_CallbackAnswersAnError_StopsWithIt()
    {
        var session = new ExportedTlsSession("a:443", new byte[64], OneByteTicket, 0, 0x0304, null, 0);
        var transfer = new FakeEasyTransfer { HeldSessions = [session, session] };
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);
        int calls = 0;

        CurlCode code = easy.SslsExport((_, _, _, _, _, _, _, _) =>
        {
            calls++;
            return CurlCode.AbortedByCallback;
        });

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void SslsExport_NoCallback_AnswersBadFunctionArgument()
    {
        var transfer = new FakeEasyTransfer { HeldSessions = [new ExportedTlsSession("a:443", new byte[64], OneByteTicket, 0, 0, null, 0)] };
        using var easy = new CurlEasy(transfer, EasyOptionDispatch.Default);

        Assert.AreEqual(CurlCode.BadFunctionArgument, easy.SslsExport(null));
    }

    [TestMethod]
    public void SslsExport_AfterCleanup_ThrowsObjectDisposed()
    {
        var easy = new CurlEasy(new FakeEasyTransfer(), EasyOptionDispatch.Default);
        easy.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => easy.SslsExport(null));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task SslsImportThenExport_OnTheManagedEngineOffWindows_GivesTheSessionBackUnderItsKey()
    {
        await using var library = CurlGlobal.Init(CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();
        var keys = new List<string?>();

        CurlCode imported = easy.SslsImport("example.test:443", null, OneByteTicketValidUntil2100);
        CurlCode exported = easy.SslsExport((_, key, _, _, _, _, _, _) =>
        {
            keys.Add(key);
            return CurlCode.Ok;
        });

        Assert.AreEqual(CurlCode.Ok, imported);
        Assert.AreEqual(CurlCode.Ok, exported);
        CollectionAssert.AreEqual(new[] { "example.test:443" }, keys);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task SslsImportAndExport_OnTheManagedEngineOnWindows_AnswerNotBuiltIn()
    {
        await using var library = CurlGlobal.Init(CurlGlobalFlags.Nothing);
        using var easy = library.EasyInit();

        Assert.AreEqual(CurlCode.NotBuiltIn, easy.SslsImport("example.test:443", null, OneByteTicket));
        Assert.AreEqual(CurlCode.NotBuiltIn, easy.SslsExport((_, _, _, _, _, _, _, _) => CurlCode.Ok));
    }
}
