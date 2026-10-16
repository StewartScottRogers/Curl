using Curl.Api.Codes;
using Curl.Protocol.Abstractions;

namespace Curl.Api.UnitTests.Codes;

[TestClass]
public sealed class CurlCodeTests
{
    // Every CURLE_* member of CURLcode in curl 8.22.0's include/curl/curl.h, in order.
    private static readonly (CurlCode Code, int Number)[] UpstreamCodes =
    [
        (CurlCode.Ok, 0), (CurlCode.UnsupportedProtocol, 1), (CurlCode.FailedInit, 2),
        (CurlCode.UrlMalformat, 3), (CurlCode.NotBuiltIn, 4), (CurlCode.CouldntResolveProxy, 5),
        (CurlCode.CouldntResolveHost, 6), (CurlCode.CouldntConnect, 7), (CurlCode.WeirdServerReply, 8),
        (CurlCode.RemoteAccessDenied, 9), (CurlCode.FtpAcceptFailed, 10), (CurlCode.FtpWeirdPassReply, 11),
        (CurlCode.FtpAcceptTimeout, 12), (CurlCode.FtpWeirdPasvReply, 13), (CurlCode.FtpWeird227Format, 14),
        (CurlCode.FtpCantGetHost, 15), (CurlCode.Http2, 16), (CurlCode.FtpCouldntSetType, 17),
        (CurlCode.PartialFile, 18), (CurlCode.FtpCouldntRetrFile, 19), (CurlCode.Obsolete20, 20),
        (CurlCode.QuoteError, 21), (CurlCode.HttpReturnedError, 22), (CurlCode.WriteError, 23),
        (CurlCode.Obsolete24, 24), (CurlCode.UploadFailed, 25), (CurlCode.ReadError, 26),
        (CurlCode.OutOfMemory, 27), (CurlCode.OperationTimedOut, 28), (CurlCode.Obsolete29, 29),
        (CurlCode.FtpPortFailed, 30), (CurlCode.FtpCouldntUseRest, 31), (CurlCode.Obsolete32, 32),
        (CurlCode.RangeError, 33), (CurlCode.Obsolete34, 34), (CurlCode.SslConnectError, 35),
        (CurlCode.BadDownloadResume, 36), (CurlCode.FileCouldntReadFile, 37), (CurlCode.LdapCannotBind, 38),
        (CurlCode.LdapSearchFailed, 39), (CurlCode.Obsolete40, 40), (CurlCode.Obsolete41, 41),
        (CurlCode.AbortedByCallback, 42), (CurlCode.BadFunctionArgument, 43), (CurlCode.Obsolete44, 44),
        (CurlCode.InterfaceFailed, 45), (CurlCode.Obsolete46, 46), (CurlCode.TooManyRedirects, 47),
        (CurlCode.UnknownOption, 48), (CurlCode.SetoptOptionSyntax, 49), (CurlCode.Obsolete50, 50),
        (CurlCode.Obsolete51, 51), (CurlCode.GotNothing, 52), (CurlCode.SslEngineNotFound, 53),
        (CurlCode.SslEngineSetFailed, 54), (CurlCode.SendError, 55), (CurlCode.RecvError, 56),
        (CurlCode.Obsolete57, 57), (CurlCode.SslCertProblem, 58), (CurlCode.SslCipher, 59),
        (CurlCode.PeerFailedVerification, 60), (CurlCode.BadContentEncoding, 61), (CurlCode.Obsolete62, 62),
        (CurlCode.FilesizeExceeded, 63), (CurlCode.UseSslFailed, 64), (CurlCode.SendFailRewind, 65),
        (CurlCode.SslEngineInitFailed, 66), (CurlCode.LoginDenied, 67), (CurlCode.TftpNotFound, 68),
        (CurlCode.TftpPerm, 69), (CurlCode.RemoteDiskFull, 70), (CurlCode.TftpIllegal, 71),
        (CurlCode.TftpUnknownId, 72), (CurlCode.RemoteFileExists, 73), (CurlCode.TftpNoSuchUser, 74),
        (CurlCode.Obsolete75, 75), (CurlCode.Obsolete76, 76), (CurlCode.SslCacertBadfile, 77),
        (CurlCode.RemoteFileNotFound, 78), (CurlCode.Ssh, 79), (CurlCode.SslShutdownFailed, 80),
        (CurlCode.Again, 81), (CurlCode.SslCrlBadfile, 82), (CurlCode.SslIssuerError, 83),
        (CurlCode.FtpPretFailed, 84), (CurlCode.RtspCseqError, 85), (CurlCode.RtspSessionError, 86),
        (CurlCode.FtpBadFileList, 87), (CurlCode.ChunkFailed, 88), (CurlCode.NoConnectionAvailable, 89),
        (CurlCode.SslPinnedPubKeyNotMatch, 90), (CurlCode.SslInvalidCertStatus, 91), (CurlCode.Http2Stream, 92),
        (CurlCode.RecursiveApiCall, 93), (CurlCode.AuthError, 94), (CurlCode.Http3, 95),
        (CurlCode.QuicConnectError, 96), (CurlCode.Proxy, 97), (CurlCode.SslClientCert, 98),
        (CurlCode.UnrecoverablePoll, 99), (CurlCode.TooLarge, 100), (CurlCode.EchRequired, 101),
    ];

    [TestMethod]
    public void Members_UpstreamTable_MatchExactlyWithUpstreamNumbers() => UpstreamCodeTable.AssertMatches(UpstreamCodes);

    [TestMethod]
    public void Members_UpstreamTable_CoverEveryNumberFromZeroTo101()
    {
        CollectionAssert.AreEqual(Enumerable.Range(0, 102).ToArray(), UpstreamCodes.Select(entry => entry.Number).ToArray());
    }

    [TestMethod]
    public void Members_WithoutAnExitCode_AreTheFifteenObsoletePlaceholders()
    {
        var exitCodeNames = Enum.GetNames<CurlExitCode>();
        var withoutExitCode = Enum.GetNames<CurlCode>().Except(exitCodeNames).ToArray();

        Assert.IsTrue(withoutExitCode.All(name => name.StartsWith("Obsolete", StringComparison.Ordinal)), string.Join(", ", withoutExitCode));
        Assert.HasCount(15, withoutExitCode);
    }

    [TestMethod]
    public void EveryExitCode_HasCurlCodeWithSameNameAndNumber()
    {
        foreach (var exitCode in Enum.GetValues<CurlExitCode>())
        {
            var code = Enum.Parse<CurlCode>(exitCode.ToString());
            Assert.AreEqual((int)exitCode, (int)code, exitCode.ToString());
        }
    }

    [TestMethod]
    public void EveryCurlCodeNamedLikeAnExitCode_HasSameNumber()
    {
        foreach (var code in Enum.GetValues<CurlCode>().Where(code => Enum.IsDefined(typeof(CurlExitCode), code.ToString())))
        {
            Assert.AreEqual((int)code, (int)Enum.Parse<CurlExitCode>(code.ToString()), code.ToString());
        }
    }

}
