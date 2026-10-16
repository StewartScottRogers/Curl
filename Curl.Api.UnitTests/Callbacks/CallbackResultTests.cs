using Curl.Api.Callbacks;

namespace Curl.Api.UnitTests.Callbacks;

/// <summary>
/// Pins every magic value libcurl's callbacks return or are handed to upstream curl 8.22.0's
/// number in <c>include/curl/curl.h</c>.
/// </summary>
[TestClass]
public sealed class CallbackResultTests
{
    [TestMethod]
    [DataRow(WriteCallbackResult.Pause, 0x10000001L)]
    [DataRow(WriteCallbackResult.Error, 0xFFFFFFFFL)]
    public void WriteCallbackResult_MatchesCurlWriteFuncValues(long constant, long upstream)
    {
        Assert.AreEqual(upstream, constant);
    }

    [TestMethod]
    [DataRow(ReadCallbackResult.Abort, 0x10000000L)]
    [DataRow(ReadCallbackResult.Pause, 0x10000001L)]
    public void ReadCallbackResult_MatchesCurlReadFuncValues(long constant, long upstream)
    {
        Assert.AreEqual(upstream, constant);
    }

    [TestMethod]
    [DataRow(ProgressCallbackResult.Continue, 0x10000001)]
    public void ProgressCallbackResult_Continue_MatchesCurlProgressFuncContinue(int constant, int upstream)
    {
        Assert.AreEqual(upstream, constant);
    }

    [TestMethod]
    [DataRow(SeekResult.Ok, 0)]
    [DataRow(SeekResult.Fail, 1)]
    [DataRow(SeekResult.CantSeek, 2)]
    public void SeekResult_MatchesCurlSeekFunc(SeekResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(SocketOptionResult.Ok, 0)]
    [DataRow(SocketOptionResult.Error, 1)]
    [DataRow(SocketOptionResult.AlreadyConnected, 2)]
    public void SocketOptionResult_MatchesCurlSockopt(SocketOptionResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(PrerequestResult.Ok, 0)]
    [DataRow(PrerequestResult.Abort, 1)]
    public void PrerequestResult_MatchesCurlPrereqFunc(PrerequestResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(TrailerResult.Ok, 0)]
    [DataRow(TrailerResult.Abort, 1)]
    public void TrailerResult_MatchesCurlTrailerFunc(TrailerResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(ChunkBeginResult.Ok, 0)]
    [DataRow(ChunkBeginResult.Fail, 1)]
    [DataRow(ChunkBeginResult.Skip, 2)]
    public void ChunkBeginResult_MatchesCurlChunkBgnFunc(ChunkBeginResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(ChunkEndResult.Ok, 0)]
    [DataRow(ChunkEndResult.Fail, 1)]
    public void ChunkEndResult_MatchesCurlChunkEndFunc(ChunkEndResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(FileNameMatchResult.Match, 0)]
    [DataRow(FileNameMatchResult.NoMatch, 1)]
    [DataRow(FileNameMatchResult.Fail, 2)]
    public void FileNameMatchResult_MatchesCurlFnmatchFunc(FileNameMatchResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(HstsResult.Ok, 0)]
    [DataRow(HstsResult.Done, 1)]
    [DataRow(HstsResult.Fail, 2)]
    public void HstsResult_MatchesCurlSts(HstsResult result, int upstream)
    {
        Assert.AreEqual(upstream, (int)result);
    }

    [TestMethod]
    [DataRow(SshKeyStatus.FineAddToFile, 0)]
    [DataRow(SshKeyStatus.Fine, 1)]
    [DataRow(SshKeyStatus.Reject, 2)]
    [DataRow(SshKeyStatus.Defer, 3)]
    [DataRow(SshKeyStatus.FineReplace, 4)]
    public void SshKeyStatus_MatchesCurlKhStat(SshKeyStatus status, int upstream)
    {
        Assert.AreEqual(upstream, (int)status);
    }

    [TestMethod]
    [DataRow(SshKeyMatch.Ok, 0)]
    [DataRow(SshKeyMatch.Mismatch, 1)]
    [DataRow(SshKeyMatch.Missing, 2)]
    public void SshKeyMatch_MatchesCurlKhMatch(SshKeyMatch match, int upstream)
    {
        Assert.AreEqual(upstream, (int)match);
    }

    [TestMethod]
    [DataRow(SshKeyType.Unknown, 0)]
    [DataRow(SshKeyType.Rsa1, 1)]
    [DataRow(SshKeyType.Rsa, 2)]
    [DataRow(SshKeyType.Dss, 3)]
    [DataRow(SshKeyType.Ecdsa, 4)]
    [DataRow(SshKeyType.Ed25519, 5)]
    public void SshKeyType_MatchesCurlKhType(SshKeyType type, int upstream)
    {
        Assert.AreEqual(upstream, (int)type);
    }

    [TestMethod]
    [DataRow(CurlInfoType.Text, 0)]
    [DataRow(CurlInfoType.HeaderIn, 1)]
    [DataRow(CurlInfoType.HeaderOut, 2)]
    [DataRow(CurlInfoType.DataIn, 3)]
    [DataRow(CurlInfoType.DataOut, 4)]
    [DataRow(CurlInfoType.SslDataIn, 5)]
    [DataRow(CurlInfoType.SslDataOut, 6)]
    public void CurlInfoType_MatchesCurlInfoTypes(CurlInfoType type, int upstream)
    {
        Assert.AreEqual(upstream, (int)type);
    }

    [TestMethod]
    [DataRow(CurlSocketType.IpConnection, 0)]
    [DataRow(CurlSocketType.Accept, 1)]
    public void CurlSocketType_MatchesCurlSockType(CurlSocketType type, int upstream)
    {
        Assert.AreEqual(upstream, (int)type);
    }

    [TestMethod]
    [DataRow(CurlFileType.File, 0)]
    [DataRow(CurlFileType.Directory, 1)]
    [DataRow(CurlFileType.SymbolicLink, 2)]
    [DataRow(CurlFileType.DeviceBlock, 3)]
    [DataRow(CurlFileType.DeviceChar, 4)]
    [DataRow(CurlFileType.NamedPipe, 5)]
    [DataRow(CurlFileType.Socket, 6)]
    [DataRow(CurlFileType.Door, 7)]
    [DataRow(CurlFileType.Unknown, 8)]
    public void CurlFileType_MatchesCurlFileType(CurlFileType type, int upstream)
    {
        Assert.AreEqual(upstream, (int)type);
    }

    [TestMethod]
    public void EveryEnum_HasOnlyThePinnedMembers()
    {
        Assert.HasCount(3, Enum.GetValues<SeekResult>());
        Assert.HasCount(3, Enum.GetValues<SocketOptionResult>());
        Assert.HasCount(2, Enum.GetValues<PrerequestResult>());
        Assert.HasCount(2, Enum.GetValues<TrailerResult>());
        Assert.HasCount(3, Enum.GetValues<ChunkBeginResult>());
        Assert.HasCount(2, Enum.GetValues<ChunkEndResult>());
        Assert.HasCount(3, Enum.GetValues<FileNameMatchResult>());
        Assert.HasCount(3, Enum.GetValues<HstsResult>());
        Assert.HasCount(5, Enum.GetValues<SshKeyStatus>());
        Assert.HasCount(3, Enum.GetValues<SshKeyMatch>());
        Assert.HasCount(6, Enum.GetValues<SshKeyType>());
        Assert.HasCount(7, Enum.GetValues<CurlInfoType>());
        Assert.HasCount(2, Enum.GetValues<CurlSocketType>());
        Assert.HasCount(9, Enum.GetValues<CurlFileType>());
    }
}
