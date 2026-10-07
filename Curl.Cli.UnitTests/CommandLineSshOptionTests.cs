using Curl.Protocol.Abstractions;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoSshOptions_LeavesThemNotGiven()
    {
        CommandLineParseResult result = Parse([Url], NoPathExists);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
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
        CommandLineParseResult result = Parse([option, file, Url], NoPathExists);

        Diagnostics.Assert("public key file", file, CommandLineParseDiagnostics.Peek(result.Options)?.SshPublicKeyFile);
        Diagnostics.Assert("warning lines", 0, result.WarningLines.Count);
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
        CommandLineParseResult result = Parse([option, value, Url], NoPathExists);

        AssertRefused(result, $"curl: option {option}: blank argument where content is expected");
    }

    [TestMethod]
    public void Parse_KnownhostsThatExists_RecordsTheFileItChecked()
    {
        string? checkedPath = null;

        CommandLineParseResult result = Parse(
            ["--knownhosts", "known_hosts", Url],
            path =>
            {
                checkedPath = path;
                return true;
            });

        Diagnostics.Act("checked path", checkedPath);
        Diagnostics.Assert("known hosts file", "known_hosts", CommandLineParseDiagnostics.Peek(result.Options)?.SshKnownHostsFile);
        Diagnostics.Assert("checked path", "known_hosts", checkedPath);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("known_hosts", result.Options.SshKnownHostsFile);
        Assert.AreEqual("known_hosts", checkedPath);
    }

    [TestMethod]
    [DataRow("b")]
    [DataRow("")]
    public void Parse_KnownhostsThatDoesNotExist_RefusesWithThreeLines(string file)
    {
        CommandLineParseResult result = Parse(["--no-compressed-ssh", "--pubkey", "a", "--knownhosts", file, Url], NoPathExists);

        AssertRefused(
            result,
            $"curl: The file '{file}' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingKnownhosts_HidesTheFileLine()
    {
        CommandLineParseResult result = Parse(["-s", "--knownhosts", "nope", Url], NoPathExists);

        AssertRefused(result, "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentShowErrorThenMissingKnownhosts_KeepsTheFileLine()
    {
        CommandLineParseResult result = Parse(["-sS", "--knownhosts", "nope", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file 'nope' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
    }

    [TestMethod]
    public void Parse_SilentThenMissingCacert_HidesTheFileLine()
    {
        CommandLineParseResult result = Parse(["-s", "--cacert", "nope", "https://127.0.0.1/x"], NoPathExists);

        AssertRefused(result, "curl: option --cacert: is badly used here");
    }

    [TestMethod]
    public void Parse_KnownhostsGivenFlagLikeFileThatDoesNotExist_RefusesAfterFileNameWarning()
    {
        CommandLineParseResult result = Parse(["--knownhosts", "-x", Url], NoPathExists);

        AssertRefused(
            result,
            "curl: The file '-x' provided to --knownhosts does not exist",
            "curl: option --knownhosts: is badly used here");
        Diagnostics.Assert(
            "warning lines",
            CommandLineParseDiagnostics.QuoteEach(["Warning: The filename argument '-x' looks like a flag."]),
            CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        CollectionAssert.AreEqual(
            new[] { "Warning: The filename argument '-x' looks like a flag." },
            result.WarningLines.ToArray());
    }

    [TestMethod]
    [DataRow(Md5)]
    [DataRow("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Parse_HostPubMd5Of32Characters_RecordsItVerbatim(string hash)
    {
        CommandLineParseResult result = Parse(["--hostpubmd5", hash, Url], NoPathExists);

        Diagnostics.Assert("host public key MD5", hash, CommandLineParseDiagnostics.Peek(result.Options)?.SshHostPublicKeyMd5);
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

        CommandLineParseResult result = Parse(arguments, NoPathExists);

        AssertRefused(result, $"curl: option {(option.EndsWith('=') ? option + hash : option)}: is badly used here");
    }

    [TestMethod]
    [DataRow("!!!")]
    [DataRow("AAAA")]
    [DataRow("-x")]
    public void Parse_HostPubSha256_RecordsAnyTextVerbatim(string hash)
    {
        CommandLineParseResult result = Parse(["--hostpubsha256", hash, Url], NoPathExists);

        Diagnostics.Assert("host public key SHA-256", hash, CommandLineParseDiagnostics.Peek(result.Options)?.SshHostPublicKeySha256);
        Diagnostics.Assert("warning lines", 0, result.WarningLines.Count);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(hash, result.Options.SshHostPublicKeySha256);
        Assert.IsEmpty(result.WarningLines);
    }

    [TestMethod]
    public void Parse_CompressedSsh_SetsSshCompression()
    {
        CommandLineParseResult result = Parse(["--compressed-ssh", Url], NoPathExists);

        Diagnostics.Assert("SSH compression", true, CommandLineParseDiagnostics.Peek(result.Options)?.SshCompression);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsTrue(result.Options.SshCompression);
    }

    [TestMethod]
    public void Parse_CompressedSshThenNoCompressedSsh_ClearsSshCompression()
    {
        CommandLineParseResult result = Parse(["--compressed-ssh", "--no-compressed-ssh", Url], NoPathExists);

        Diagnostics.Assert("SSH compression", false, CommandLineParseDiagnostics.Peek(result.Options)?.SshCompression);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsFalse(result.Options.SshCompression);
    }

    [TestMethod]
    public void Parse_KeyKeyTypeAndPassWithSftpUrl_RecordTheSshPrivateKey()
    {
        CommandLineParseResult result = Parse(
            ["--key", "id_ed25519", "--key-type", "PEM", "--pass", "secret", "--pubkey", "id_ed25519.pub", "--knownhosts", "kh", Url],
            EveryPathExists);

        CommandLineOptions? options = CommandLineParseDiagnostics.Peek(result.Options);
        Diagnostics.Act("private key", options?.PrivateKey);
        Diagnostics.Act("private key type", options?.PrivateKeyType);
        Diagnostics.Act("passphrase", options?.Passphrase);
        Diagnostics.Assert("private key", "id_ed25519", options?.PrivateKey);
        Diagnostics.Assert("private key type", "PEM", options?.PrivateKeyType);
        Diagnostics.Assert("passphrase", "secret", options?.Passphrase);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual("id_ed25519", result.Options.PrivateKey);
        Assert.AreEqual("PEM", result.Options.PrivateKeyType);
        Assert.AreEqual("secret", result.Options.Passphrase);
        Assert.AreEqual("id_ed25519.pub", result.Options.SshPublicKeyFile);
        Assert.AreEqual("kh", result.Options.SshKnownHostsFile);
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments, Func<string, bool> pathExists)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments, pathExists);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("public key file", result.Options.SshPublicKeyFile);
            Diagnostics.Act("known hosts file", result.Options.SshKnownHostsFile);
            Diagnostics.Act("host public key MD5", result.Options.SshHostPublicKeyMd5);
            Diagnostics.Act("host public key SHA-256", result.Options.SshHostPublicKeySha256);
            Diagnostics.Act("SSH compression", result.Options.SshCompression);
        }

        return result;
    }

    private void AssertRefused(CommandLineParseResult result, params string[] expectedLinesBeforeTryHelp)
    {
        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine));
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            expectedLinesBeforeTryHelp.Append(CommandLineRefusal.TryHelpLine).ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
    }
}
