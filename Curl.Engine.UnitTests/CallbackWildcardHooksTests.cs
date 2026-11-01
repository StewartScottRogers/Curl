using Curl.Api.Callbacks;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Original.Protocol.Ftp;

namespace Curl.Engine;

[TestClass]
public sealed class CallbackWildcardHooksTests
{
    private static readonly FtpFileListEntry Link = new()
    {
        FileName = "c.txt",
        FileType = FtpFileType.SymbolicLink,
        TimeText = "Jan 11 10:00",
        Permissions = 0x1FF,
        PermissionsText = "rwxrwxrwx",
        HardLinks = 1,
        User = "u",
        Group = "g",
        Size = 4,
        Target = "real",
    };

    [TestMethod]
    public void Of_WildcardMatchOff_ReturnsNull()
    {
        EasyOptions options = new() { Callbacks = new() { ChunkBegin = (_, _) => ChunkBeginResult.Ok } };

        Assert.IsNull(CallbackWildcardHooks.Of(options));
    }

    [TestMethod]
    public void Of_WildcardMatchOnWithNoCallback_BehavesAsLibcurlsDefaults()
    {
        IFtpWildcardHooks hooks = CallbackWildcardHooks.Of(new EasyOptions { Behaviour = new EasyBehaviourOptions { WildcardMatch = true } })!;

        Assert.AreEqual(FtpNameMatchAnswer.Match, hooks.MatchName("*.txt", "a.txt"));
        Assert.AreEqual(FtpNameMatchAnswer.NoMatch, hooks.MatchName("*.txt", "a.md"));
        Assert.AreEqual(FtpChunkBeginAnswer.Transfer, hooks.BeginChunk(Link, 1));
        Assert.AreEqual(FtpChunkEndAnswer.CarryOn, hooks.EndChunk());
    }

    [TestMethod]
    public void FileInfoOf_ListingEntry_CarriesEveryFieldTheParserFills()
    {
        CurlFileInfo info = CallbackWildcardHooks.FileInfoOf(Link);

        Assert.AreEqual(
            new CurlFileInfo
            {
                FileName = "c.txt",
                FileType = CurlFileType.SymbolicLink,
                TimeText = "Jan 11 10:00",
                Permissions = 0x1FF,
                PermissionsText = "rwxrwxrwx",
                HardLinks = 1,
                User = "u",
                Group = "g",
                Size = 4,
                Target = "real",
            },
            info);
        Assert.IsNull(info.Time);
        Assert.IsNull(info.UserId);
        Assert.IsNull(info.GroupId);
    }

    [TestMethod]
    [DataRow(FileNameMatchResult.Match, FtpNameMatchAnswer.Match)]
    [DataRow(FileNameMatchResult.NoMatch, FtpNameMatchAnswer.NoMatch)]
    [DataRow(FileNameMatchResult.Fail, FtpNameMatchAnswer.Fail)]
    public void MatchName_CallbackSet_AnswersItsResultAndPassesThePatternAndName(FileNameMatchResult result, FtpNameMatchAnswer expected)
    {
        string? seen = null;
        CallbackWildcardHooks hooks = new((pattern, name) =>
        {
            seen = pattern + " " + name;
            return result;
        }, null, null, FtpDownloadEveryMatch.Instance);

        Assert.AreEqual(expected, hooks.MatchName("x*", "xy"));
        Assert.AreEqual("x* xy", seen);
    }

    [TestMethod]
    [DataRow(ChunkBeginResult.Ok, FtpChunkBeginAnswer.Transfer)]
    [DataRow(ChunkBeginResult.Fail, FtpChunkBeginAnswer.Fail)]
    [DataRow(ChunkBeginResult.Skip, FtpChunkBeginAnswer.Skip)]
    [DataRow((ChunkBeginResult)7, FtpChunkBeginAnswer.Transfer)]
    public void BeginChunk_CallbackSet_MapsItsAnswerAsWcStatemachsSwitchDoes(ChunkBeginResult result, FtpChunkBeginAnswer expected)
    {
        CurlFileInfo? seen = null;
        int seenRemains = 0;
        CallbackWildcardHooks hooks = new(null, (file, remains) =>
        {
            seen = file;
            seenRemains = remains;
            return result;
        }, null, FtpDownloadEveryMatch.Instance);

        Assert.AreEqual(expected, hooks.BeginChunk(Link, 3));
        Assert.AreEqual(CallbackWildcardHooks.FileInfoOf(Link), seen);
        Assert.AreEqual(3, seenRemains);
    }

    [TestMethod]
    [DataRow(ChunkEndResult.Ok, FtpChunkEndAnswer.CarryOn)]
    [DataRow(ChunkEndResult.Fail, FtpChunkEndAnswer.Fail)]
    public void EndChunk_CallbackSet_AnswersItsResult(ChunkEndResult result, FtpChunkEndAnswer expected)
    {
        CallbackWildcardHooks hooks = new(null, null, () => result, FtpDownloadEveryMatch.Instance);

        Assert.AreEqual(expected, hooks.EndChunk());
    }
}
