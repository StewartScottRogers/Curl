using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records the SSH options <c>--pubkey</c>, <c>--knownhosts</c>, <c>--hostpubmd5</c>,
/// <c>--hostpubsha256</c> and <c>--compressed-ssh</c>, and that <c>--key</c>, <c>--key-type</c> and
/// <c>--pass</c> reach the same options for SSH. Every refusal was measured with the local curl 8.21.0
/// through <c>Record-CurlExchange.ps1 -NoServer</c> against <c>sftp://127.0.0.1/x</c> on 2026-09-28; the
/// bytes are in BL-562's Notes. The <c>--knownhosts</c> existence check runs against a fake, never the disk.
/// </summary>
[TestClass]
public sealed class CommandLineSshOptionTests
{
    private const string Url = "sftp://127.0.0.1/x";

    private const string Md5 = "0123456789ABCDEF0123456789abcdef";

    private static readonly Func<string, bool> EveryPathExists = _ => true;

    private static readonly Func<string, bool> NoPathExists = _ => false;

    [TestMethod]
    public void Parse_NoSshOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.SshPublicKeyFile);
        Assert.IsNull(result.Options.SshKnownHostsFile);
        Assert.IsNull(result.Options.SshHostPublicKeyMd5);
        Assert.IsNull(result.Options.SshHostPublicKeySha256);
        Assert.IsFalse(result.Options.SshCompression);
    }

    [TestMethod]
    [DataRow("--pubkey", "id.pub")]
    [DataRow("--pubkey", "-x")]
    public void Parse_Pubkey_RecordsTheFileWithoutWarning(string option, string file)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, file, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(file, result.Options.SshPublicKeyFile);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    [DataRow("--pubkey", "")]
    [DataRow("--hostpubmd5", "")]
    [DataRow("--hostpubsha256", "")]
    public void Parse_BlankSshText_RefusesAsBlank(string option, string value)
    {
        CommandLineParseResult result = CommandLineParser.Parse([option, value, Url], NoPathExists);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_KnownhostsThatExists_RecordsTheFileItChecked()
    {
        string? checkedPath = null;

        CommandLineParseResult result = CommandLineParser.Parse(
            ["--knownhosts", "known_hosts", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("known_hosts", result.Options.SshKnownHostsFile);
        Assert.AreEqual("known_hosts", checkedPath);
    }

    [TestMethod]
    [DataRow("b")]
    [DataRow("")]
    public void Parse_KnownhostsThatDoesNotExist_RefusesWithThreeLines(string file)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-compressed-ssh", "--pubkey", "a", "--knownhosts", file, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingKnownhosts_HidesTheFileLine()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--knownhosts", "nope", Url], NoPathExists);

        AssertRefused(result, "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentShowErrorThenMissingKnownhosts_KeepsTheFileLine()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-sS", "--knownhosts", "nope", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file 'nope' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingCacert_HidesTheFileLine()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["-s", "--cacert", "nope", "https://127.0.0.1/x"], NoPathExists);

        AssertRefused(result, "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_KnownhostsGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--knownhosts", "-x", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '-x' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow(Md5)]
    [DataRow("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Parse_HostPubMd5Of32Characters_RecordsItVerbatim(string hash)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hostpubmd5", hash, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(hash, result.Options.SshHostPublicKeyMd5);
    }

    [TestMethod]
    [DataRow("--hostpubmd5", "abc")]
    [DataRow("--hostpubmd5", "-x")]
    [DataRow("--hostpubmd5", Md5 + "0")]
    [DataRow("--hostpubmd5=", "abc")]
    public void Parse_HostPubMd5NotOf32Characters_RefusesAsBadlyUsed(string option, string hash)
    {
        string[] arguments = option.EndsWith('=') ? ["-s", option + hash, Url] : ["-s", option, hash, Url];

        CommandLineParseResult result = CommandLineParser.Parse(arguments, NoPathExists);

        AssertRefused(result, $"curl: option {(option.EndsWith('=') ? option + hash : option)}: is badly used here");
    }

    [TestMethod]
    [DataRow("!!!")]
    [DataRow("AAAA")]
    [DataRow("-x")]
    public void Parse_HostPubSha256_RecordsAnyTextVerbatim(string hash)
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--hostpubsha256", hash, Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(hash, result.Options.SshHostPublicKeySha256);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_CompressedSsh_SetsSshCompression()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--compressed-ssh", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.SshCompression);
    }

    [TestMethod]
    public void Parse_CompressedSshThenNoCompressedSsh_ClearsSshCompression()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--compressed-ssh", "--no-compressed-ssh", Url], NoPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.SshCompression);
    }

    [TestMethod]
    public void Parse_KeyKeyTypeAndPassWithSftpUrl_RecordTheSshPrivateKey()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--key", "id_ed25519", "--key-type", "PEM", "--pass", "secret", "--pubkey", "id_ed25519.pub", "--knownhosts", "kh", Url],
            EveryPathExists);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("id_ed25519", result.Options.PrivateKey);
        Assert.AreEqual("PEM", result.Options.PrivateKeyType);
        Assert.AreEqual("secret", result.Options.Passphrase);
        Assert.AreEqual("id_ed25519.pub", result.Options.SshPublicKeyFile);
        Assert.AreEqual("kh", result.Options.SshKnownHostsFile);
    }

    private static void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
