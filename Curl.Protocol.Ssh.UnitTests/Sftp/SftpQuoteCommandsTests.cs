using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpQuoteCommands" /> against an in-memory peer: the SFTP request each
/// <c>-Q</c> command sends, byte for byte, and its outcome, as measured 2026-09-29 with
/// curl 8.21.0 (libssh2 1.11.1, Schannel build) against OpenSSH 10.2's <c>sftp-server</c>
/// logging every request, and for a 64-bit C <c>long</c> with Ubuntu's curl 8.18.0
/// (libssh2 1.11.1, OpenSSL), whose quote code is the same (BL-572, ADR-0247).
/// </summary>
[TestClass]
public sealed class SftpQuoteCommandsTests
{
    private const uint NoSuchFile = 2;

    private const uint Failure = 4;

    // What STAT answers in these tests: size 2, owner 1001, group 1002, mode 0100644 and
    // two times.
    private const uint StatAccessTime = 1_600_000_000;

    private const uint StatModifyTime = 1_700_000_000;

    private static readonly byte[] Home = Encoding.UTF8.GetBytes(SftpServerScript.Home);

    private static readonly byte[] StatAttributes = Join(UInt32(0x0F), UInt32(0), UInt32(2), UInt32(1001), UInt32(1002), UInt32(0x81A4), UInt32(StatAccessTime), UInt32(StatModifyTime));

    [TestMethod]
    public async Task RunBeforeTransferAsync_Chmod_SendsSetstatOfThePermissionsAloneAsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok), "chmod 640 /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SetStatRequest(0, "/f", Join(UInt32(4), UInt32(0x1A0))));
    }

    [TestMethod]
    [DataRow("chown 1000 /f", 1000u, 1002u, DisplayName = "chown keeps the group, as measured")]
    [DataRow("chgrp 1000 /f", 1001u, 1000u, DisplayName = "chgrp keeps the owner, as measured")]
    [DataRow("chown 4294967295 /f", 4294967295u, 1002u, DisplayName = "the largest 32-bit number")]
    [DataRow("chgrp 12abc /f", 1001u, 12u, DisplayName = "digits up to the first that is not")]
    public async Task RunBeforeTransferAsync_ChownOrChgrp_StatsThenSetsTheOwnerAndGroupAsMeasured(string value, uint userId, uint groupId)
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes).Status(1, SftpStatusCode.Ok), value);

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(2), UInt32(userId), UInt32(groupId))));
    }

    [TestMethod]
    [DataRow("atime \"Thu, 02 Jan 2020 03:04:05 GMT\" /f", 1_577_934_245u, StatModifyTime, DisplayName = "atime keeps the modification time, as measured")]
    [DataRow("mtime \"Tue, 1 Jan 2019 10:00:00 GMT\" /f", StatAccessTime, 1_546_336_800u, DisplayName = "mtime keeps the access time, as measured")]
    public async Task RunBeforeTransferAsync_AtimeOrMtime_StatsThenSetsBothTimesAsMeasured(string value, uint accessTime, uint modifyTime)
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes).Status(1, SftpStatusCode.Ok), value);

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(8), UInt32(accessTime), UInt32(modifyTime))));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_DatePast32BitsWith64BitLong_SendsItsLow32BitsAsMeasuredOnLinux()
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes).Status(1, SftpStatusCode.Ok), cLongIs32Bits: false, "mtime \"1 Jan 2200\" /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(8), UInt32(StatAccessTime), UInt32(2_963_151_104))));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_DateBeforeTheEpoch_SendsItsLow32Bits()
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes).Status(1, SftpStatusCode.Ok), "mtime \"31 Dec 1969 23:59:58 GMT\" /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(8), UInt32(StatAccessTime), UInt32(uint.MaxValue - 1))));
    }

    [TestMethod]
    [DataRow("chmod 7777 /f", 0xFFFu, DisplayName = "the largest mode")]
    [DataRow("chmod 12x /f", 10u, DisplayName = "octal digits up to the first that is not")]
    public async Task RunBeforeTransferAsync_ChmodNumber_ReadsItAsOctal(string value, uint mode)
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok), value);

        outcome.AssertSucceeded();
        outcome.AssertRequests(SetStatRequest(0, "/f", Join(UInt32(4), UInt32(mode))));
    }

    [TestMethod]
    [DataRow("chmod 999 /f", "Syntax error: chmod permissions not a number", DisplayName = "not octal, as measured")]
    [DataRow("chmod 17777 /f", "Syntax error: chmod permissions not a number", DisplayName = "past 07777, as measured")]
    [DataRow("*chmod x /f", "Syntax error: chmod permissions not a number", DisplayName = "an asterisk does not pass it over")]
    public async Task RunBeforeTransferAsync_ChmodNotANumber_FailsBeforeAnyRequestAsMeasured(string value, string message)
    {
        Outcome outcome = await RunAsync(Script(), value);

        outcome.AssertFailed(CurlExitCode.QuoteError, message);
        outcome.AssertRequests();
    }

    [TestMethod]
    [DataRow("chgrp abc /f", true, "Syntax error: chgrp gid not a number", DisplayName = "chgrp, as measured")]
    [DataRow("chown x1 /f", true, "Syntax error: chown uid not a number", DisplayName = "chown")]
    [DataRow("chown 4294967296 /f", true, "Syntax error: chown uid not a number", DisplayName = "past 32 bits")]
    [DataRow("chown 1000 /f", false, "Syntax error: chown uid not a number", DisplayName = "every number with a 64-bit long, as measured on Linux")]
    [DataRow("chgrp 0 /f", false, "Syntax error: chgrp gid not a number", DisplayName = "even 0 with a 64-bit long, as measured on Linux")]
    [DataRow("atime 2020-01-02T03:04:05Z /f", true, "incorrect date format for atime", DisplayName = "a date curl cannot read, as measured")]
    [DataRow("mtime nonsense /f", true, "incorrect date format for mtime", DisplayName = "mtime")]
    [DataRow("mtime \"1 Jan 2200\" /f", true, "date overflow", DisplayName = "past 32 bits with a 32-bit long, as measured")]
    public async Task RunBeforeTransferAsync_BadValue_FailsAfterTheStatAsMeasured(string value, bool cLongIs32Bits, string message)
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes), cLongIs32Bits, value);

        outcome.AssertFailed(CurlExitCode.QuoteError, message);
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_ChgrpNotANumberWithAsterisk_SetsTheStatAttributesAsTheyAreAsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, StatAttributes).Status(1, SftpStatusCode.Ok), "*chgrp abc /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", StatAttributes));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatWithExtendedAttributes_ReadsPastThemAndSendsNone()
    {
        byte[] extended = Join(UInt32(0x80000004), UInt32(0x81A4), UInt32(1), Name("n"), Name("v"));
        Outcome outcome = await RunAsync(Script().Attributes(0, extended).Status(1, SftpStatusCode.Ok), "*chown x /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(4), UInt32(0x81A4))));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatFails_FailsWithItsDescriptionAsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Status(0, NoSuchFile), "chown 1000 /zz");

        outcome.AssertFailed(CurlExitCode.QuoteError, "Attempt to get SFTP stats failed: No such file or directory");
        outcome.AssertRequests(SftpServerScript.StatRequest("/zz", 0));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatAndSetstatFailWithAsterisk_SetsTheNewOwnerAndGroup0AsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Status(0, NoSuchFile).Status(1, NoSuchFile), "*chown 1000 /zz");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/zz", 0), SetStatRequest(1, "/zz", Join(UInt32(2), UInt32(1000), UInt32(0))));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatAnsweredOk_TakesItAsEmptyAttributesAsLibssh2Does()
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok).Status(1, SftpStatusCode.Ok), "chgrp 7 /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(SftpServerScript.StatRequest("/f", 0), SetStatRequest(1, "/f", Join(UInt32(2), UInt32(0), UInt32(7))));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_SetstatFails_FailsNamingThePathAsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Status(0, NoSuchFile), "chmod 644 /zz");

        outcome.AssertFailed(CurlExitCode.QuoteError, "Attempt to set SFTP stats for \"/zz\" failed: No such file or directory");
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_SetstatAnsweredWithAttributes_SucceedsAsLibssh2Does()
    {
        Outcome outcome = await RunAsync(Script().Attributes(0, UInt32(0)), "chmod 644 /f");

        outcome.AssertSucceeded();
    }

    [TestMethod]
    [DataRow("mkdir /d", DisplayName = "mkdir, as measured")]
    [DataRow("rmdir /d", DisplayName = "rmdir, as measured")]
    [DataRow("rm /f", DisplayName = "rm, as measured")]
    [DataRow("rename /a /b", DisplayName = "rename, as measured")]
    [DataRow("ln /a /b", DisplayName = "ln, as measured")]
    [DataRow("symlink /a /b", DisplayName = "symlink, as measured")]
    public async Task RunBeforeTransferAsync_PathCommand_SendsItsRequestAsMeasured(string value)
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok), value);

        outcome.AssertSucceeded();
        outcome.AssertRequests(value.Split(' ')[0] switch
        {
            "mkdir" => SftpServerScript.MakeDirectoryRequest("/d", 0),
            "rmdir" => PathRequest(SftpPacketType.RemoveDirectory, 0, "/d"),
            "rm" => PathRequest(SftpPacketType.Remove, 0, "/f"),
            "rename" => PathRequest(SftpPacketType.Rename, 0, "/a", "/b"),
            _ => PathRequest(SftpPacketType.SymbolicLink, 0, "/a", "/b"),
        });
    }

    [TestMethod]
    [DataRow("mkdir /d", Failure, "mkdir \"/d\" failed: Operation failed", DisplayName = "mkdir of an existing directory, as measured")]
    [DataRow("rmdir /zz", NoSuchFile, "rmdir \"/zz\" failed: No such file or directory", DisplayName = "rmdir, as measured")]
    [DataRow("rmdir /~/", 3u, "rmdir \"/home/fake/\" failed: Permission denied", DisplayName = "rmdir of the home directory, as measured")]
    [DataRow("rm /zz", NoSuchFile, "rm \"/zz\" failed: No such file or directory", DisplayName = "rm, as measured")]
    [DataRow("rename /zz /yy", NoSuchFile, "rename \"/zz\" to \"/yy\" failed: No such file or directory", DisplayName = "rename, as measured")]
    [DataRow("ln /b /dd", Failure, "symlink \"/b\" to \"/dd\" failed: Operation failed", DisplayName = "ln, as measured")]
    [DataRow("rm /zz", 1u, "rm \"/zz\" failed: Unknown error in libssh2", DisplayName = "a status libssh2 has no text for")]
    public async Task RunBeforeTransferAsync_PathCommandFails_FailsWithExit21AndCurlsMessageAsMeasured(string value, uint status, string message)
    {
        Outcome outcome = await RunAsync(Script().Status(0, status), value);

        outcome.AssertFailed(CurlExitCode.QuoteError, message);
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_FailureWithAsterisk_GoesOnToTheNextCommandAsMeasured()
    {
        Outcome outcome = await RunAsync(Script().Status(0, NoSuchFile).Status(1, SftpStatusCode.Ok), "*rm /zz", "rm /f");

        outcome.AssertSucceeded();
        outcome.AssertRequests(PathRequest(SftpPacketType.Remove, 0, "/zz"), PathRequest(SftpPacketType.Remove, 1, "/f"));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_SecondCommandFails_SendsNoThird()
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok).Status(1, NoSuchFile), "rm /a", "rm /b", "rm /c");

        outcome.AssertFailed(CurlExitCode.QuoteError, "rm \"/b\" failed: No such file or directory");
        outcome.AssertRequests(PathRequest(SftpPacketType.Remove, 0, "/a"), PathRequest(SftpPacketType.Remove, 1, "/b"));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_SecondCommandMalformed_FailsAfterTheFirstRan()
    {
        Outcome outcome = await RunAsync(Script().Status(0, SftpStatusCode.Ok), "rm /a", "foo bar");

        outcome.AssertFailed(CurlExitCode.QuoteError, "Unknown SFTP command");
        outcome.AssertRequests(PathRequest(SftpPacketType.Remove, 0, "/a"));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_ConnectionEndsBeforeTheAnswer_FailsWithExit79()
    {
        Outcome outcome = await RunAsync(Script(), "rm /a");

        outcome.AssertFailed(CurlExitCode.Ssh, "Error in the SSH layer");
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_Pwd_WritesTheTransfersPathAsAHeaderLineAsMeasured()
    {
        Outcome outcome = await RunAsync(Script(), "pwd", "*PWD");

        outcome.AssertSucceeded();
        outcome.AssertRequests();
        Assert.AreEqual("257 \"/home/fake/a.txt\" is current directory.\n257 \"/home/fake/a.txt\" is current directory.\n", outcome.Header);
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_PwdWithNoHeaderOutput_WritesNothing()
    {
        ScriptedConnection connection = new(Script().Bytes);
        SftpSession session = await SftpSession.StartAsync(SftpSessionTests.Transport(connection), CancellationToken.None);

        await new SftpQuoteCommands(["pwd"], [], null, cLongIs32Bits: true).RunBeforeTransferAsync(session, Home, "/p"u8.ToArray(), CancellationToken.None);

        Assert.HasCount(1, SftpServerScript.SftpRequests(connection.Written));
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_Statvfs_SendsTheOpenSshExtensionAndWritesItsFieldsAsMeasured()
    {
        ulong[] fields = [4096, 4096, 263940717, 233486291, 220060423, 67108864, 66840330, 66840330, 15601863864774658782, 0xFF, 255];
        byte[] reply = Join([SftpPacketType.ExtendedReply], UInt32(0), Join([.. fields.Select(UInt64)]));
        Outcome outcome = await RunAsync(Script().Sftp(reply), "statvfs /home/fake/files");

        outcome.AssertSucceeded();
        outcome.AssertRequests(Join([SftpPacketType.Extended], UInt32(0), Name("statvfs@openssh.com"), Name("/home/fake/files")));
        Assert.AreEqual(
            "statvfs:\nf_bsize: 4096\nf_frsize: 4096\nf_blocks: 263940717\nf_bfree: 233486291\nf_bavail: 220060423\nf_files: 67108864\n" +
            "f_ffree: 66840330\nf_favail: 66840330\nf_fsid: 15601863864774658782\nf_flag: 3\nf_namemax: 255\n",
            outcome.Header,
            "f_flag keeps only ST_RDONLY and ST_NOSUID, as libssh2 does");
    }

    [TestMethod]
    [DataRow(NoSuchFile, "statvfs \"/zz\" failed: No such file or directory", DisplayName = "missing, as measured")]
    [DataRow(SftpStatusCode.Ok, "statvfs \"/zz\" failed: Unknown error in libssh2", DisplayName = "OK is no answer to libssh2")]
    public async Task RunBeforeTransferAsync_StatvfsAnsweredWithAStatus_FailsAsMeasured(uint status, string message)
    {
        Outcome outcome = await RunAsync(Script().Status(0, status), "statvfs /zz");

        outcome.AssertFailed(CurlExitCode.QuoteError, message);
        Assert.AreEqual(string.Empty, outcome.Header);
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatvfsFailsWithAsterisk_WritesNothingAndGoesOn()
    {
        Outcome outcome = await RunAsync(Script().Status(0, NoSuchFile).Status(1, SftpStatusCode.Ok), "*statvfs /zz", "rm /f");

        outcome.AssertSucceeded();
        Assert.AreEqual(string.Empty, outcome.Header);
    }

    [TestMethod]
    public async Task RunBeforeTransferAsync_StatvfsAnswerCutShort_FailsWithExit79()
    {
        Outcome outcome = await RunAsync(Script().Sftp(Join([SftpPacketType.ExtendedReply], UInt32(0), UInt32(1))), "statvfs /");

        outcome.AssertFailed(CurlExitCode.Ssh, "Error in the SSH layer");
    }

    [TestMethod]
    public void From_Context_SortsTheValuesAsTheCurlToolDoes()
    {
        MemoryStream header = new();
        TransferContext context = new()
        {
            Url = CurlUrl.Parse("sftp://h/f"),
            Output = new MemoryStream(),
            HeaderOutput = header,
            QuoteCommands = ["rm /a", "-rm /b", "+rm /c", "*-rm /d", "-*rm /e"],
        };

        SftpQuoteCommands quotes = SftpQuoteCommands.From(context, cLongIs32Bits: true);

        Assert.AreEqual("rm /a|*-rm /d", string.Join('|', quotes.BeforeTransfer));
        Assert.AreEqual("rm /b|*rm /e", string.Join('|', quotes.AfterTransfer));
    }

    [TestMethod]
    public void None_HasNoCommands()
    {
        Assert.IsEmpty(SftpQuoteCommands.None.BeforeTransfer);
        Assert.IsEmpty(SftpQuoteCommands.None.AfterTransfer);
    }

    internal static byte[] PathRequest(byte type, uint id, params string[] paths) =>
        Join([type], UInt32(id), Join([.. paths.Select(Name)]));

    private static byte[] SetStatRequest(uint id, string path, byte[] attributes) =>
        Join([SftpPacketType.SetStat], UInt32(id), Name(path), attributes);

    private static byte[] UInt64(ulong value) => Join(UInt32((uint)(value >> 32)), UInt32((uint)value));

    private static SftpServerScript Script() => SftpServerScript.Started();

    private static Task<Outcome> RunAsync(SftpServerScript script, params string[] commands) => RunAsync(script, cLongIs32Bits: true, commands);

    private static async Task<Outcome> RunAsync(SftpServerScript script, bool cLongIs32Bits, params string[] commands)
    {
        ScriptedConnection connection = new(script.Bytes);
        MemoryStream header = new();
        SftpSession session = await SftpSession.StartAsync(SftpSessionTests.Transport(connection), CancellationToken.None);
        SftpQuoteCommands quotes = new(commands, [], header, cLongIs32Bits);
        SshTransferException? failure = null;
        try
        {
            await quotes.RunBeforeTransferAsync(session, Home, "/home/fake/a.txt"u8.ToArray(), CancellationToken.None);
        }
        catch (SshTransferException exception)
        {
            failure = exception;
        }

        return new Outcome(failure, [.. SftpServerScript.SftpRequests(connection.Written).Skip(1)], Encoding.UTF8.GetString(header.ToArray()));
    }

    private sealed record Outcome(SshTransferException? Failure, List<byte[]> Requests, string Header)
    {
        internal void AssertSucceeded() => Assert.IsNull(Failure, Failure?.Message);

        internal void AssertFailed(CurlExitCode exitCode, string message)
        {
            Assert.IsNotNull(Failure);
            Assert.AreEqual(exitCode, Failure.ExitCode);
            Assert.AreEqual(message, Failure.Message);
        }

        internal void AssertRequests(params byte[][] expected)
        {
            Assert.HasCount(expected.Length, Requests);
            for (int index = 0; index < expected.Length; index++)
            {
                CollectionAssert.AreEqual(expected[index], Requests[index], $"request {index}");
            }
        }
    }
}
