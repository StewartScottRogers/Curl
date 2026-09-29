using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ssh.Sftp;

/// <summary>
/// Pins how <see cref="SftpQuoteCommand" /> reads an SFTP <c>-Q</c> command, as measured
/// 2026-09-29 with curl 8.21.0 (libssh2 1.11.1, Schannel build) against OpenSSH 10.2's
/// <c>sftp-server</c> logging every request (BL-572, ADR-0247).
/// </summary>
[TestClass]
public sealed class SftpQuoteCommandTests
{
    private static readonly byte[] Home = "/home/fake"u8.ToArray();

    [TestMethod]
    [DataRow("chgrp 1000 /f", nameof(SftpQuoteOperation.ChangeGroup), "1000", "/f")]
    [DataRow("chmod 640 /f", nameof(SftpQuoteOperation.ChangeMode), "640", "/f")]
    [DataRow("chown 1000 /f", nameof(SftpQuoteOperation.ChangeOwner), "1000", "/f")]
    [DataRow("atime \"Thu, 02 Jan 2020 03:04:05 GMT\" /f", nameof(SftpQuoteOperation.SetAccessTime), "Thu, 02 Jan 2020 03:04:05 GMT", "/f")]
    [DataRow("mtime 20190101 /f", nameof(SftpQuoteOperation.SetModifyTime), "20190101", "/f")]
    [DataRow("ln /a /b", nameof(SftpQuoteOperation.SymbolicLink), "/a", "/b")]
    [DataRow("symlink /a /b", nameof(SftpQuoteOperation.SymbolicLink), "/a", "/b")]
    [DataRow("mkdir /d", nameof(SftpQuoteOperation.MakeDirectory), "/d", "")]
    [DataRow("rename /a /b", nameof(SftpQuoteOperation.Rename), "/a", "/b")]
    [DataRow("rmdir /d", nameof(SftpQuoteOperation.RemoveDirectory), "/d", "")]
    [DataRow("rm /f", nameof(SftpQuoteOperation.Remove), "/f", "")]
    [DataRow("statvfs /", nameof(SftpQuoteOperation.StatFileSystem), "/", "")]
    public void Parse_EachCommandCurlKnows_ReadsItsOperationAndPaths(string value, string operation, string first, string second)
    {
        SftpQuoteCommand command = SftpQuoteCommand.Parse(value, Home);

        Assert.AreEqual(Enum.Parse<SftpQuoteOperation>(operation), command.Operation);
        Assert.IsFalse(command.IgnoresFailure);
        Assert.AreEqual(value, command.Text);
        Assert.AreEqual(first, Encoding.UTF8.GetString(command.FirstPath));
        Assert.AreEqual(second, Encoding.UTF8.GetString(command.SecondPath));
    }

    [TestMethod]
    [DataRow("pwd", DisplayName = "lower case")]
    [DataRow("PWD", DisplayName = "upper case, as measured")]
    public void Parse_Pwd_StandsAloneInAnyCase(string value)
    {
        SftpQuoteCommand command = SftpQuoteCommand.Parse(value, Home);

        Assert.AreEqual(SftpQuoteOperation.PrintWorkingDirectory, command.Operation);
        Assert.IsEmpty(command.FirstPath);
    }

    [TestMethod]
    public void Parse_LeadingAsterisk_IgnoresFailureAndDropsItFromTheText()
    {
        SftpQuoteCommand command = SftpQuoteCommand.Parse("*rm /zz", Home);

        Assert.IsTrue(command.IgnoresFailure);
        Assert.AreEqual("rm /zz", command.Text);
        Assert.AreEqual(SftpQuoteOperation.Remove, command.Operation);
    }

    [TestMethod]
    [DataRow("rm \"/f/b c.txt\"", "/f/b c.txt", DisplayName = "double quotes, as measured")]
    [DataRow("rm '/f/b c.txt'", "/f/b c.txt", DisplayName = "single quotes, as measured")]
    [DataRow("rm \"a\\\"b\\\\c\\'d\"", "a\"b\\c'd", DisplayName = "escaped quote, backslash and apostrophe")]
    [DataRow("rm \"/~/x\"", "/~/x", DisplayName = "no home directory inside quotes")]
    [DataRow("rm /~/x", "/home/fake/x", DisplayName = "home directory, as measured")]
    [DataRow("rmdir /~/", "/home/fake/", DisplayName = "the home directory alone, as measured")]
    [DataRow("rm  \t /f \t ", "/f", DisplayName = "blanks around the path")]
    [DataRow("rm /f\tg", "/f\tg", DisplayName = "a tab inside a word")]
    public void Parse_Path_ReadsItAsCurlGetPathnameDoes(string value, string expected)
    {
        SftpQuoteCommand command = SftpQuoteCommand.Parse(value, Home);

        Assert.AreEqual(expected, Encoding.UTF8.GetString(command.FirstPath));
    }

    [TestMethod]
    [DataRow("foo", "Syntax error command 'foo', missing parameter", DisplayName = "no space, as measured")]
    [DataRow("rm", "Syntax error command 'rm', missing parameter", DisplayName = "no argument, as measured")]
    [DataRow("rm\t/f", "Syntax error command 'rm\t/f', missing parameter", DisplayName = "a tab is no space, as measured")]
    [DataRow("rm ", "Syntax error: Bad first parameter to 'rm '", DisplayName = "empty argument, as measured")]
    [DataRow("rm \"\"", "Syntax error: Bad first parameter to 'rm \"\"'", DisplayName = "empty quotes, as measured")]
    [DataRow("rm \"a\\b\"", "Syntax error: Bad first parameter to 'rm \"a\\b\"'", DisplayName = "bad escape, as measured")]
    [DataRow("rm \"a\\", "Syntax error: Bad first parameter to 'rm \"a\\'", DisplayName = "escape at the end")]
    [DataRow("rm \"abc", "Syntax error: Bad first parameter to 'rm \"abc'", DisplayName = "unclosed quote")]
    [DataRow("*foo bar", "Unknown SFTP command", DisplayName = "unknown, even with an asterisk, as measured")]
    [DataRow("foo bar", "Unknown SFTP command", DisplayName = "unknown, as measured")]
    [DataRow("chmod 644", "Syntax error in chmod 644: Bad second parameter", DisplayName = "attribute with one argument, as measured")]
    [DataRow("ln /a", "Syntax error in ln/symlink: Bad second parameter", DisplayName = "ln with one argument, as measured")]
    [DataRow("rename /a", "Syntax error in rename: Bad second parameter", DisplayName = "rename with one argument, as measured")]
    [DataRow("rm /f more", "Suspicious data after the command line", DisplayName = "a second argument, as measured")]
    [DataRow("rm \"/f\"x", "Suspicious data after the command line", DisplayName = "text after the quote, as measured")]
    [DataRow("rm /~/b c", "Suspicious data after the command line", DisplayName = "home path with a space, as measured")]
    [DataRow("ln /a /b /c", "Suspicious data after the command line", DisplayName = "a third argument")]
    public void Parse_Malformed_FailsWithExit21AndCurlsMessage(string value, string expected)
    {
        SshTransferException failure = Assert.ThrowsExactly<SshTransferException>(() => SftpQuoteCommand.Parse(value, Home));

        Assert.AreEqual(CurlExitCode.QuoteError, failure.ExitCode);
        Assert.AreEqual(expected, failure.Message);
    }
}
