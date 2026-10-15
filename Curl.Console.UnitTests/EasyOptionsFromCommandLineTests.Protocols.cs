using Curl.Api.Options.Protocols;
using Curl.Cli;
using FtpClearCommandChannel = Curl.Api.Options.Protocols.FtpClearCommandChannel;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyOptionsFromCommandLine.ProtocolOf" />: each FTP, SSH, mail, TFTP and TELNET
/// command-line option given sets its <see cref="EasyProtocolOptions" /> member, and a command line
/// without it keeps libcurl's default (ADR-0529).
/// </summary>
public sealed partial class EasyOptionsFromCommandLineTests
{
    private static readonly EasyProtocolOptions LibcurlProtocolDefaults = new();

    // --knownhosts refuses a file that is not there, so it names this assembly.
    private static readonly string ExistingFile = typeof(EasyOptionsFromCommandLineTests).Assembly.Location;

    public static IEnumerable<object?[]> GivenProtocolOptions =>
    [
        GivenProtocol("-P", o => o.Ftp.ActiveAddress, "192.0.2.1", "-P", "192.0.2.1"),
        GivenProtocol("--ftp-pasv", o => o.Ftp.ActiveAddress, null, "-P", "-", "--ftp-pasv"),
        GivenProtocol("-Q", o => string.Join('|', o.Ftp.Quote), "NOOP|*SITE x", "-Q", "NOOP", "-Q", "+TYPE A", "-Q", "*SITE x", "-Q", "-DELE f"),
        GivenProtocol("-Q +", o => string.Join('|', o.Ftp.PreQuote), "TYPE A", "-Q", "NOOP", "-Q", "+TYPE A", "-Q", "-DELE f"),
        GivenProtocol("-Q -", o => string.Join('|', o.Ftp.PostQuote), "DELE f", "-Q", "NOOP", "-Q", "+TYPE A", "-Q", "-DELE f"),
        GivenProtocol("-a", o => o.Ftp.Append, true, "-a"),
        GivenProtocol("--disable-eprt", o => o.Ftp.UseEprt, false, "--disable-eprt"),
        GivenProtocol("--disable-epsv", o => o.Ftp.UseEpsv, false, "--disable-epsv"),
        GivenProtocol("--ftp-pret", o => o.Ftp.UsePret, true, "--ftp-pret"),
        GivenProtocol("--ftp-create-dirs", o => o.Ftp.CreateMissingDirectories, FtpCreateMissingDirectories.CreateAndRetry, "--ftp-create-dirs"),
        GivenProtocol("--ftp-alternative-to-user", o => o.Ftp.AlternativeToUser, "SITE AUTH", "--ftp-alternative-to-user", "SITE AUTH"),
        GivenProtocol("--no-ftp-skip-pasv-ip", o => o.Ftp.SkipPasvIp, false, "--no-ftp-skip-pasv-ip"),
        GivenProtocol("--ftp-ssl-ccc", o => o.Ftp.ClearCommandChannel, FtpClearCommandChannel.Passive, "--ftp-ssl-ccc"),
        GivenProtocol("--ftp-ssl-ccc-mode active", o => o.Ftp.ClearCommandChannel, FtpClearCommandChannel.Active, "--ftp-ssl-ccc-mode", "active"),
        GivenProtocol("--ftp-account", o => o.Ftp.Account, "acct", "--ftp-account", "acct"),
        GivenProtocol("--ftp-method multicwd", o => o.Ftp.FileMethod, FtpFileMethod.MultiCwd, "--ftp-method", "multicwd"),
        GivenProtocol("--ftp-method nocwd", o => o.Ftp.FileMethod, FtpFileMethod.NoCwd, "--ftp-method", "nocwd"),
        GivenProtocol("--ftp-method singlecwd", o => o.Ftp.FileMethod, FtpFileMethod.SingleCwd, "--ftp-method", "singlecwd"),
        GivenProtocol("--key", o => o.Ssh.PrivateKeyFile, "id_rsa", "--key", "id_rsa"),
        GivenProtocol("--pubkey", o => o.Ssh.PublicKeyFile, "id_rsa.pub", "--pubkey", "id_rsa.pub"),
        GivenProtocol("--hostpubmd5", o => o.Ssh.HostPublicKeyMd5, "00112233445566778899aabbccddeeff", "--hostpubmd5", "00112233445566778899aabbccddeeff"),
        GivenProtocol("--hostpubsha256", o => o.Ssh.HostPublicKeySha256, "AAAA", "--hostpubsha256", "AAAA"),
        GivenProtocol("--knownhosts", o => o.Ssh.KnownHostsFile, ExistingFile, "--knownhosts", ExistingFile),
        GivenProtocol("--compressed-ssh", o => o.Ssh.Compression, true, "--compressed-ssh"),
        GivenProtocol("--mail-from", o => o.Smtp.MailFrom, "<a@example.com>", "--mail-from", "<a@example.com>"),
        GivenProtocol("--mail-rcpt", o => string.Join('|', o.Smtp.MailRecipients), "b@example.com|c@example.com", "--mail-rcpt", "b@example.com", "--mail-rcpt", "c@example.com"),
        GivenProtocol("--mail-rcpt-allowfails", o => o.Smtp.MailRecipientAllowFails, true, "--mail-rcpt-allowfails"),
        GivenProtocol("--mail-auth", o => o.Smtp.MailAuth, "a@example.com", "--mail-auth", "a@example.com"),
        GivenProtocol("--tftp-blksize", o => o.Tftp.BlockSize, 1024, "--tftp-blksize", "1024"),
        GivenProtocol("--tftp-no-options", o => o.Tftp.NoOptions, true, "--tftp-no-options"),
        GivenProtocol("-t", o => string.Join('|', o.Telnet.TelnetOptions), "TTYPE=vt100|XDISPLOC=x:0", "-t", "TTYPE=vt100", "-t", "XDISPLOC=x:0"),
    ];

    public static IEnumerable<object?[]> AbsentProtocolOptions =>
    [
        AbsentProtocol("Ftp.ActiveAddress", o => o.Ftp.ActiveAddress),
        AbsentProtocol("Ftp.Quote", o => o.Ftp.Quote.Count),
        AbsentProtocol("Ftp.PreQuote", o => o.Ftp.PreQuote.Count),
        AbsentProtocol("Ftp.PostQuote", o => o.Ftp.PostQuote.Count),
        AbsentProtocol("Ftp.Append", o => o.Ftp.Append),
        AbsentProtocol("Ftp.UseEprt", o => o.Ftp.UseEprt),
        AbsentProtocol("Ftp.UseEpsv", o => o.Ftp.UseEpsv),
        AbsentProtocol("Ftp.UsePret", o => o.Ftp.UsePret),
        AbsentProtocol("Ftp.CreateMissingDirectories", o => o.Ftp.CreateMissingDirectories),
        AbsentProtocol("Ftp.AlternativeToUser", o => o.Ftp.AlternativeToUser),
        AbsentProtocol("Ftp.SkipPasvIp", o => o.Ftp.SkipPasvIp),
        AbsentProtocol("Ftp.SslAuthentication", o => o.Ftp.SslAuthentication),
        AbsentProtocol("Ftp.ClearCommandChannel", o => o.Ftp.ClearCommandChannel),
        AbsentProtocol("Ftp.Account", o => o.Ftp.Account),
        AbsentProtocol("Ftp.FileMethod", o => o.Ftp.FileMethod),
        AbsentProtocol("Ssh.AuthenticationTypes", o => o.Ssh.AuthenticationTypes),
        AbsentProtocol("Ssh.Compression", o => o.Ssh.Compression),
        AbsentProtocol("Ssh.HostPublicKeyMd5", o => o.Ssh.HostPublicKeyMd5),
        AbsentProtocol("Ssh.HostPublicKeySha256", o => o.Ssh.HostPublicKeySha256),
        AbsentProtocol("Ssh.PublicKeyFile", o => o.Ssh.PublicKeyFile),
        AbsentProtocol("Ssh.PrivateKeyFile", o => o.Ssh.PrivateKeyFile),
        AbsentProtocol("Ssh.KnownHostsFile", o => o.Ssh.KnownHostsFile),
        AbsentProtocol("Smtp.MailFrom", o => o.Smtp.MailFrom),
        AbsentProtocol("Smtp.MailRecipients", o => o.Smtp.MailRecipients.Count),
        AbsentProtocol("Smtp.MailAuth", o => o.Smtp.MailAuth),
        AbsentProtocol("Smtp.MailRecipientAllowFails", o => o.Smtp.MailRecipientAllowFails),
        AbsentProtocol("Tftp.BlockSize", o => o.Tftp.BlockSize),
        AbsentProtocol("Tftp.NoOptions", o => o.Tftp.NoOptions),
        AbsentProtocol("Telnet.TelnetOptions", o => o.Telnet.TelnetOptions.Count),
        AbsentProtocol("Rtsp", o => o.Rtsp),
        AbsentProtocol("WebSocket", o => o.WebSocket),
    ];

    [TestMethod]
    [DynamicData(nameof(GivenProtocolOptions))]
    public void ProtocolOf_OptionGiven_SetsItsMember(string option, Func<EasyProtocolOptions, object?> read, object? expected, string[] arguments)
    {
        EasyProtocolOptions protocol = EasyOptionsFromCommandLine.ProtocolOf(Parse(arguments));

        Assert.AreEqual(expected, read(protocol), option);
    }

    [TestMethod]
    [DynamicData(nameof(AbsentProtocolOptions))]
    public void ProtocolOf_OptionAbsent_KeepsLibcurlDefault(string member, Func<EasyProtocolOptions, object?> read)
    {
        EasyProtocolOptions protocol = EasyOptionsFromCommandLine.ProtocolOf(Parse());

        Assert.AreEqual(read(LibcurlProtocolDefaults), read(protocol), member);
    }

    [TestMethod]
    public void ProtocolOf_Key_IsAlsoTheTlsKey()
    {
        CommandLineOptions options = Parse("--key", "id_rsa");

        Assert.AreEqual(EasyOptionsFromCommandLine.TlsOf(options).Origin.Key, EasyOptionsFromCommandLine.ProtocolOf(options).Ssh.PrivateKeyFile);
    }

    [TestMethod]
    public void ProtocolOf_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => EasyOptionsFromCommandLine.ProtocolOf(null!));
    }

    private static object?[] GivenProtocol(string option, Func<EasyProtocolOptions, object?> read, object? expected, params string[] arguments) =>
        [option, read, expected, arguments];

    private static object?[] AbsentProtocol(string member, Func<EasyProtocolOptions, object?> read) => [member, read];
}
