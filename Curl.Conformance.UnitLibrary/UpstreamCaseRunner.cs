using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>
/// Runs one vendored upstream test case through curl in process, the way <c>runtests.pl</c> at
/// <c>curl-8_21_0</c> runs it through the <c>curl</c> binary (ADR-0013, decision 4): expand the
/// file, skip it with a reason when the harness cannot run it, write its <c>&lt;client&gt;</c>
/// files, run the command against the in-memory <c>sws</c> emulation, and compare what came out
/// with <c>&lt;verify&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The command gets the arguments <c>runtests.pl</c> puts before it: <c>--output
/// %LOGDIR/curl%TESTNUMBER.out</c> unless the command's <c>option</c> says <c>no-output</c> or the case
/// verifies <c>&lt;stdout&gt;</c> without <c>force-output</c>, then <c>--include</c> unless it says
/// <c>no-include</c>, then <c>--trace-ascii %LOGDIR/trace%TESTNUMBER</c> (<c>--trace</c> when it says
/// <c>binary-trace</c>), <c>--trace-config all</c> and <c>--trace-time</c>, as upstream's do: no case
/// compares the trace file, but a trace option makes curl print the default config file's
/// <c>Note: Read config file from</c> line that test433 verifies (BL-2017).
/// </para>
/// <para>
/// <c>%LOGDIR</c> is the absolute log directory with forward slashes, so cases can run in
/// parallel, <c>%PWD</c> is the tests directory the caller names, with forward slashes, and <c>%FILE_PWD</c> is empty, so <c>file://localhost%FILE_PWD/%LOGDIR/…</c> still
/// names the file. <see cref="UpstreamTestDirectoryComposition"/> first rewrites <c>%PWD/%LOGDIR</c>
/// to <c>%LOGDIR</c>, and <c>%SRCDIR</c> before the two emulated <c>libtest</c> scripts to <c>.</c>;
/// any other <c>%SRCDIR</c> has no value. As upstream does, standard output and standard error are saved to
/// <c>%LOGDIR/stdout%TESTNUMBER</c> and <c>%LOGDIR/stderr%TESTNUMBER</c> before the comparison,
/// for the cases that verify them as files. <c>%include</c> and <c>%includetext</c> read the file
/// they name by its path, relative to the working directory when not absolute, as nothing when it
/// is not there. <c>%HOSTIP</c> and <c>%CLIENTIP</c> are <c>127.0.0.1</c>, <c>%HTTPPORT</c> is
/// <see cref="HttpPort"/>, <c>%HOST6IP</c> is <c>[::1]</c>, <c>%HTTP6PORT</c> is <see cref="Http6Port"/>, <c>%RESOLVE</c> is the name
/// <see cref="UpstreamResolveCheck"/> emulates in a precheck, <c>%PROXYPORT</c> is <see cref="ProxyPort"/>, <c>%FTPPORT</c> is <see cref="FtpPort"/>, <c>%SMTPPORT</c> is <see cref="SmtpPort"/>, <c>%IMAPPORT</c> is <see cref="ImapPort"/>, <c>%POP3PORT</c> is <see cref="Pop3Port"/>, <c>%SOCKSPORT</c> is <see cref="SocksPort"/>, <c>%MQTTPORT</c> is <see cref="MqttPort"/>, <c>%TFTPPORT</c> is <see cref="TftpPort"/>,<c>%RTSPPORT</c> is <see cref="RtspPort"/>, <c>%HTTPSPORT</c>, <c>%SMTPSPORT</c>, <c>%IMAPSPORT</c>, <c>%POP3SPORT</c> and <c>%FTPSPORT</c> are <see cref="HttpsPort"/>, <see cref="SmtpsPort"/>, <see cref="ImapsPort"/>, <see cref="Pop3sPort"/> and <see cref="FtpsPort"/> when a certificate directory is named, <c>%NOLISTENPORT</c> is <see cref="NoListenPort"/>, a port that refuses every connection, <c>%FTPTIME2</c> is <c>8</c> (the seconds upstream gives FTP time-out cases on a host whose FTP server checks in under a second), and <c>%VERSION</c> is <see cref="CurlVersion"/>. Every other variable
/// is unknown, so a case that uses one is skipped.
/// </para>
/// </remarks>
/// <param name="runCurl">Runs curl for one invocation and returns its exit code.</param>
/// <param name="platform">The features and null device of the platform Curl runs on.</param>
/// <param name="timeProvider">Measures <paramref name="timeLimit"/>.</param>
/// <param name="timeLimit">How long a run may take before the case fails.</param>
/// <param name="sshServer">
/// The stand-in for upstream's test <c>sshd</c> (BL-1916), or <see langword="null"/> to leave
/// <c>%SSHPORT</c>, <c>%USER</c>, <c>%SFTP_PWD</c>, <c>%SCP_PWD</c>, <c>%SSHSRVMD5</c> and
/// <c>%SSHSRVSHA256</c> without a value, so the SSH cases skip. Given one, <c>%SSHPORT</c> is
/// <see cref="UpstreamSshServer.SshPort"/>, <c>%USER</c> its user, <c>%SFTP_PWD</c> and
/// <c>%SCP_PWD</c> are empty (as <c>%FILE_PWD</c>, since <c>%LOGDIR</c> is already absolute), the
/// two fingerprints are its host key's, and its client key files are written to
/// <c>%LOGDIR/server/</c> before the run.
/// </param>
public sealed class UpstreamCaseRunner(
    Func<UpstreamCurlInvocation, Task<int>> runCurl,
    UpstreamCurlPlatform platform,
    TimeProvider timeProvider,
    TimeSpan timeLimit,
    UpstreamSshServer? sshServer = null)
{
    /// <summary>The value of <c>%HTTPPORT</c>; every connection reaches the emulation whatever its port.</summary>
    public const string HttpPort = "8990";

    /// <summary>
    /// The value of <c>%HTTP6PORT</c>, upstream's <c>http-ipv6</c> server on <c>%HOST6IP</c>: connections to
    /// <c>[::1]</c> at this port reach the same sws emulation as <see cref="HttpPort"/>, all in memory, so no
    /// case needs IPv6 from the machine.
    /// </summary>
    public const string Http6Port = "8991";

    /// <summary>The value of <c>%PROXYPORT</c>: connections to this port reach the emulation too, recorded apart for <c>&lt;verify&gt;&lt;proxy&gt;</c>.</summary>
    public const string ProxyPort = "8992";

    /// <summary>The value of <c>%FTPPORT</c>: connections to this port reach the FTP control channel, <see cref="FtpServerConnector"/>, whose commands follow the sws emulation's received bytes for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string FtpPort = "8993";

    /// <summary>The value of <c>%SMTPPORT</c>: connections to this port reach the SMTP server, <see cref="SmtpServerConnector"/>, whose command lines follow the FTP control channel's for <c>&lt;verify&gt;&lt;protocol&gt;</c> and whose last message is compared with <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public const string SmtpPort = "8995";

    /// <summary>The value of <c>%IMAPPORT</c>: connections to this port reach the IMAP server, <see cref="ImapServerConnector"/>, whose command lines follow the SMTP server's for <c>&lt;verify&gt;&lt;protocol&gt;</c> and whose last <c>APPEND</c> literal is compared with <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public const string ImapPort = "8997";

    /// <summary>The value of <c>%POP3PORT</c>: connections to this port reach the POP3 server, <see cref="Pop3ServerConnector"/>, whose command lines follow the IMAP server's for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string Pop3Port = "8999";

    /// <summary>The value of <c>%SOCKSPORT</c>: connections to this port reach the socksd emulation, <see cref="SocksServerConnector"/>.</summary>
    public const string SocksPort = "8994";

    /// <summary>The value of <c>%MQTTPORT</c>: connections to this port reach the mqttd emulation, <see cref="MqttServerConnector"/>, whose protocol dump follows the sws emulation's received bytes for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string MqttPort = "8998";

    /// <summary>
    /// The value of <c>%RTSPPORT</c>. Every case naming it at <c>curl-8_21_0</c> is a <c>&lt;tool&gt;</c>
    /// libtest, which screening skips for that reason, so no RTSP server stands behind it (ADR-0457).
    /// </summary>
    public const string RtspPort = "8996";

    /// <summary>The value of <c>%TFTPPORT</c>: datagrams to this port reach the tftpd emulation, <see cref="TftpServerConnector"/>, whose request dump follows the mqttd emulation's for <c>&lt;verify&gt;&lt;protocol&gt;</c>.</summary>
    public const string TftpPort = "9003";

    /// <summary>The value of <c>%HTTPSPORT</c> when the caller names a certificate directory: connections to this port reach the sws emulation through TLS, <see cref="HttpsServerConnector"/>, presenting the certificate the case's <c>https</c> server line names from <c>%CERTDIR/certs/</c>.</summary>
    public const string HttpsPort = "8989";

    /// <summary>The value of <c>%SMTPSPORT</c> when the caller names a certificate directory: connections to this port reach the SMTP server through TLS, <see cref="MailTlsServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string SmtpsPort = "9000";

    /// <summary>The value of <c>%IMAPSPORT</c> when the caller names a certificate directory: connections to this port reach the IMAP server through TLS, <see cref="MailTlsServerConnector"/>.</summary>
    public const string ImapsPort = "9001";

    /// <summary>The value of <c>%POP3SPORT</c> when the caller names a certificate directory: connections to this port reach the POP3 server through TLS, <see cref="MailTlsServerConnector"/>.</summary>
    public const string Pop3sPort = "9002";

    /// <summary>The value of <c>%FTPSPORT</c> when the caller names a certificate directory: connections to this port reach the FTP control channel through implicit TLS, <see cref="FtpsServerConnector"/>, presenting <c>%CERTDIR/certs/test-localhost.pem</c>.</summary>
    public const string FtpsPort = "9007";

    /// <summary>The value of <c>%NOLISTENPORT</c>: connections to this port are refused, <see cref="NoListenPortConnector"/>.</summary>
    public const string NoListenPort = "47";

    /// <summary>The value of <c>%VERSION</c>: the curl release Curl matches.</summary>
    public const string CurlVersion = "8.21.0";

    private const string HostAddress = "127.0.0.1";

    // runtests.pl's $HOST6IP: the IPv6 loopback, bracketed for a URL.
    private const string Host6Address = "[::1]";

    private static readonly string[] ClientFileParts = ["file", "file1", "file2", "file3", "file4"];

    // What runtests.pl's open reads: nothing when the file is not there.
    private static readonly Func<string, byte[]?> ReadIncludedFile = path => File.Exists(path) ? File.ReadAllBytes(path) : null;

    /// <summary>Runs one case.</summary>
    /// <param name="testNumber">The case's number, from its file name.</param>
    /// <param name="testFile">The case's file, as vendored.</param>
    /// <param name="logDirectory">
    /// An empty directory of the case's own, <c>%LOGDIR</c>, by an absolute path with no blank in it;
    /// the caller deletes it.
    /// </param>
    /// <param name="testsDirectory">
    /// The release's <c>tests</c> folder, <c>%PWD</c>, by an absolute path with no blank in it, as
    /// <c>runtests.pl</c>'s working directory; <see langword="null"/> leaves <c>%PWD</c> without a
    /// value, so a case that uses it is skipped.
    /// </param>
    /// <param name="certificateDirectory">
    /// The folder holding upstream's <c>certs</c> folder, <c>%CERTDIR</c> (cases name
    /// <c>%CERTDIR/certs/test-ca.crt</c>), by an absolute path with no blank in it;
    /// <see langword="null"/> leaves <c>%CERTDIR</c> without a value, so a case that uses it is skipped.
    /// </param>
    /// <returns>Whether the case passed, failed with its first difference, or was skipped with its reason.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="logDirectory"/>, <paramref name="testsDirectory"/> or
    /// <paramref name="certificateDirectory"/> holds a blank, which would split every command that
    /// names <c>%LOGDIR</c>, <c>%PWD</c> or <c>%CERTDIR</c> unquoted (test3009's
    /// <c>--output-dir %PWD/not-there</c>, GF-0044), as upstream's relative <c>log/</c> never does.
    /// </exception>
    public async Task<UpstreamCaseOutcome> RunAsync(
        int testNumber, ReadOnlyMemory<byte> testFile, string logDirectory, string? testsDirectory = null, string? certificateDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(logDirectory);
        RefuseABlank(logDirectory, "log directory", nameof(logDirectory));
        RefuseABlank(testsDirectory ?? string.Empty, "tests directory", nameof(testsDirectory));
        RefuseABlank(certificateDirectory ?? string.Empty, "certificate directory", nameof(certificateDirectory));

        string logDirectoryVariable = logDirectory.Replace('\\', '/');
        Dictionary<string, string> variables = Variables(testNumber, logDirectoryVariable);
        AddDirectoryVariable(variables, "PWD", testsDirectory);
        AddDirectoryVariable(variables, "CERTDIR", certificateDirectory);
        AddTlsPorts(variables, certificateDirectory);
        AddSshVariables(variables);

        UpstreamTestFileExpansion expansion = UpstreamTestFileExpander.Expand(UpstreamTestDirectoryComposition.Rewrite(testFile.Span), variables, platform.Features, ReadIncludedFile);
        UpstreamTestCaseParseResult parsed = expansion.Parse();
        if (!parsed.IsParsed)
        {
            return UpstreamCaseOutcome.Skipped(parsed.Failure.Message);
        }

        return (UpstreamCaseScreening.FindSkipReason(expansion, parsed.TestCase, platform.Features)
                ?? UpstreamCaseScreening.FindFileOutsideLogDirectory(parsed.TestCase, logDirectory)) is { } reason
            ? UpstreamCaseOutcome.Skipped(reason)
            : await RunScreenedAsync(parsed.TestCase, testNumber, logDirectoryVariable, certificateDirectory).ConfigureAwait(false);
    }

    private static void RefuseABlank(string directory, string description, string parameterName)
    {
        if (directory.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException($"The {description} {directory} holds a blank; upstream's commands name it unquoted.", parameterName);
        }
    }

    private static void AddDirectoryVariable(Dictionary<string, string> variables, string name, string? directory)
    {
        if (directory is not null)
        {
            variables[name] = directory.Replace('\\', '/');
        }
    }

    // Upstream's stunnel-fronted servers present a certificate from %CERTDIR/certs/, so their ports have a value only with it.
    private static void AddTlsPorts(Dictionary<string, string> variables, string? certificateDirectory)
    {
        if (certificateDirectory is not null)
        {
            variables["HTTPSPORT"] = HttpsPort;
            variables["SMTPSPORT"] = SmtpsPort;
            variables["IMAPSPORT"] = ImapsPort;
            variables["POP3SPORT"] = Pop3sPort;
            variables["FTPSPORT"] = FtpsPort;
        }
    }

    private void AddSshVariables(Dictionary<string, string> variables)
    {
        if (sshServer is not null)
        {
            variables["SSHPORT"] = UpstreamSshServer.SshPort.ToString(CultureInfo.InvariantCulture);
            variables["USER"] = sshServer.User;
            variables["SFTP_PWD"] = string.Empty;
            variables["SCP_PWD"] = string.Empty;
            variables["SSHSRVMD5"] = sshServer.HostKeyMd5;
            variables["SSHSRVSHA256"] = sshServer.HostKeySha256;
        }
    }

    // sshserver.pl writes the client's key pair into the server's log folder before the case runs.
    // Upstream's sshd runs the real scp and sftp-server, which write an upload where the URL names it; the SCP and SFTP
    // cases name %LOGDIR/upload.%TESTNUMBER, the file runtests.pl compares with <verify><upload>.
    private byte[] SshUpload(string logDirectory, int testNumber)
    {
        string upload = $"{logDirectory}/upload.{testNumber.ToString(CultureInfo.InvariantCulture)}";
        return sshServer is not null && File.Exists(upload) ? File.ReadAllBytes(upload) : [];
    }

    private void WriteSshClientKeyFiles(string logDirectory)
    {
        if (sshServer is not null)
        {
            Directory.CreateDirectory($"{logDirectory}/server");
            File.WriteAllBytes($"{logDirectory}/server/curl_client_key", sshServer.ClientPrivateKeyFile.ToArray());
            File.WriteAllBytes($"{logDirectory}/server/curl_client_key.pub", sshServer.ClientPublicKeyFile.ToArray());
        }
    }

    private Dictionary<string, string> Variables(int testNumber, string logDirectory) =>
        new(StringComparer.Ordinal)
        {
            ["HOSTIP"] = HostAddress,
            ["CLIENTIP"] = HostAddress,
            ["HTTPPORT"] = HttpPort,
            ["PROXYPORT"] = ProxyPort,
            ["SOCKSPORT"] = SocksPort,
            ["FTPPORT"] = FtpPort,
            ["SMTPPORT"] = SmtpPort,
            ["IMAPPORT"] = ImapPort,
            ["POP3PORT"] = Pop3Port,
            ["MQTTPORT"] = MqttPort,
            ["RTSPPORT"] = RtspPort,
            ["TFTPPORT"] = TftpPort,
            ["NOLISTENPORT"] = NoListenPort,
            // servers.pm's $ftpchecktime * 8, where the check time is never below 1 second.
            ["FTPTIME2"] = "8",
            ["TESTNUMBER"] = testNumber.ToString(CultureInfo.InvariantCulture),
            ["LOGDIR"] = logDirectory,
            ["FILE_PWD"] = string.Empty,
            ["VERSION"] = CurlVersion,
            ["DEV_NULL"] = platform.NullDevice,
            ["PERL"] = UpstreamPerlOneLiner.Program,
            ["RESOLVE"] = UpstreamResolveCheck.Program,
            ["HOST6IP"] = Host6Address,
            ["HTTP6PORT"] = Http6Port,
        };

    private async Task<UpstreamCaseOutcome> RunScreenedAsync(UpstreamTestCase testCase, int testNumber, string logDirectory, string? certificateDirectory)
    {
        if (PrecheckSkipReason(testCase) is { } skipReason)
        {
            return UpstreamCaseOutcome.Skipped(skipReason);
        }

        string outputFile = $"{logDirectory}/curl{testNumber.ToString(CultureInfo.InvariantCulture)}.out";
        WriteSshClientKeyFiles(logDirectory);
        WriteClientFiles(testCase);
        List<string> arguments = Arguments(testCase, outputFile, $"{logDirectory}/trace{testNumber.ToString(CultureInfo.InvariantCulture)}");

        // On the real clock the server's waits make a case take seconds that a loaded machine
        // stretches past the time limit (test1677's writedelay: 5.5 seconds, BL-1355); with no
        // curl timer to race them, they are skipped.
        TimeProvider serverClock = ServerClock(arguments);
        SwsHttpServerConnector server = new(testCase, serverClock);
        TftpServerConnector tftp = new(testCase, serverClock);
        // Not disposed: CurlCommandRunner.RunAsync takes no cancellation token, so a run past the
        // time limit is stopped only at its next exchange with the abandoned server, and may write
        // to these until then. A memory stream holds nothing but its buffer, which the collector takes.
        MemoryStream standardOutput = new();
        MemoryStream standardError = new();
        MemoryStream standardInput = new(StandardInput(testCase));
        FtpServerConnector ftp = new(testCase, new NoListenPortConnector(HttpsServer(testCase, server, certificateDirectory)), serverClock);
        SmtpServerConnector smtp = new(testCase, ftp);
        ImapServerConnector imap = new(testCase, smtp);
        Pop3ServerConnector pop3 = new(testCase, imap);
        MqttServerConnector mqtt = new(testCase, new SocksServerConnector(testCase, MailTlsServer(testCase, FtpsServer(testCase, pop3, certificateDirectory), certificateDirectory)));
        IConnector servers = sshServer?.InFrontOf(mqtt) ?? mqtt;
        // A CONNECT through the http-proxy to a mail port or the FTP control or passive port reaches that stand-in
        // (test1319 to 1321, BL-2011; test714 and test715, BL-2035).
        server.TunnelServerForPort = port => port is Pop3ServerConnector.Pop3Port or ImapServerConnector.ImapPort or SmtpServerConnector.SmtpPort or FtpServerConnector.FtpPort or FtpServerConnector.PassivePort ? pop3 : null;
        UpstreamCurlInvocation invocation = new(arguments, standardOutput, standardError, standardInput, servers, tftp, EnvironmentVariables(testCase)) { ConnectionListener = ftp.ActiveModeListener };
        (int exitCode, string? failure) = await RunCurlAsync(invocation, server).ConfigureAwait(false);
        if (failure is not null)
        {
            return UpstreamCaseOutcome.Failed(failure);
        }

        File.WriteAllBytes($"{logDirectory}/stdout{testNumber}", standardOutput.ToArray());
        File.WriteAllBytes($"{logDirectory}/stderr{testNumber}", standardError.ToArray());
        // Read before the postcheck, as before: only the SSH upload is read after it.
        byte[] receivedBytes = [.. server.ReceivedBytes.Span, .. ftp.ReceivedBytes.Span, .. smtp.ProtocolLog.Span, .. imap.ProtocolLog.Span, .. pop3.ProtocolLog.Span, .. mqtt.ProtocolLog.Span, .. tftp.ProtocolLog.Span];
        byte[] outputFileBytes = ReadOutputFile(outputFile);
        return Judge(testCase, () => new UpstreamCaseRun(exitCode, standardOutput.ToArray(), standardError.ToArray(), receivedBytes, outputFileBytes)
        {
            ProxyReceivedBytes = server.ProxyReceivedBytes.ToArray(),
            // A case reaches one uploading server, so at most one of these holds an upload.
            UploadedBytes = [.. smtp.UploadedMessage.Span, .. imap.UploadedMessage.Span, .. tftp.UploadedBytes.Span, .. ftp.UploadedBytes.Span, .. SshUpload(logDirectory, testNumber)],
        });
    }

    // runtests.pl runs the postcheck, then compares <verify><upload>, so the run is built after the postcheck: it can
    // move the upload into place (tests 624 and 625 upload into a folder and test610.pl moves the file).
    private UpstreamCaseOutcome Judge(UpstreamTestCase testCase, Func<UpstreamCaseRun> buildRun)
    {
        if (FirstFailedCheck(testCase, "verify", "postcheck", result => result.ExitCode != 0) is { } postcheck)
        {
            return UpstreamCaseOutcome.Failed($"postcheck FAILED: exit code {postcheck.ExitCode.ToString(CultureInfo.InvariantCulture)}");
        }

        return UpstreamCaseVerification.FindFirstDifference(testCase, buildRun()) is { } difference
            ? UpstreamCaseOutcome.Failed(difference)
            : UpstreamCaseOutcome.Passed;
    }

    // runtests.pl sets each NAME=value line of every <client><setenv> part (getpart joins parts of one
    // name, as test433 has two) for the run, an empty value as an
    // empty variable, and unsets a NAME with no '='. The run's environment holds nothing else, so
    // nothing is left to restore after it.
    private static Dictionary<string, string> EnvironmentVariables(UpstreamTestCase testCase)
    {
        Dictionary<string, string> variables = new(StringComparer.Ordinal);
        foreach (string line in testCase.FindAll("client", "setenv").SelectMany(UpstreamTestPartBodies.Lines))
        {
            int equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                variables[line[..equals]] = line[(equals + 1)..];
            }
        }

        return variables;
    }

    // A case naming the https server (`https` or `https <certificate file>`) reaches it on %HTTPSPORT, which has a value only with a certificate directory.
    private static IConnector HttpsServer(UpstreamTestCase testCase, SwsHttpServerConnector server, string? certificateDirectory) =>
        UpstreamCaseScreening.HttpsCertificateFile(testCase) is { } certificateFile && certificateDirectory is not null
            ? new HttpsServerConnector(server, HttpsServerConnector.LoadCertificate(Path.Combine(certificateDirectory, "certs", certificateFile)), server)
            : server;

    // A case naming an implicit-TLS mail server (smtps, imaps or pop3s) reaches it on its port, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
    private static IConnector MailTlsServer(UpstreamTestCase testCase, IConnector mail, string? certificateDirectory) =>
        certificateDirectory is not null && UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).Intersect(MailTlsServerConnector.EmulatedServers).Any()
            ? new MailTlsServerConnector(HttpsServerConnector.LoadCertificate(Path.Combine(certificateDirectory, "certs", HttpsServerConnector.DefaultCertificateFile)), mail)
            : mail;

    // A case naming the ftps server reaches it on %FTPSPORT, which has a value only with a certificate directory; stunnel presents test-localhost.pem.
    private static IConnector FtpsServer(UpstreamTestCase testCase, IConnector ftp, string? certificateDirectory) =>
        certificateDirectory is not null && UpstreamTestPartBodies.Lines(testCase.Find("client", "server")).Intersect(FtpsServerConnector.EmulatedServers).Any()
            ? new FtpsServerConnector(HttpsServerConnector.LoadCertificate(Path.Combine(certificateDirectory, "certs", HttpsServerConnector.DefaultCertificateFile)), ftp)
            : ftp;

    private static TimeProvider ServerClock(List<string> arguments) =>
        CurlTimerOptions.AnyIn(arguments) ? TimeProvider.System : new WaitSkippingTimeProvider();

    private static byte[] ReadOutputFile(string outputFile) => File.Exists(outputFile) ? File.ReadAllBytes(outputFile) : [];

    // Any exception curl lets escape is a failure of the case, not of the harness. Curl starts on
    // a thread of its own: against the in-memory server every await can complete at once, so a
    // run that loops would otherwise never return its task, and the time limit would never start.
    // Past the limit the server is abandoned, which stops such a loop at its next exchange.
    // Not the thread pool: a queued work item can wait past the limit on a busy machine
    // (BL-1056), and a looping curl holding a pool thread starves the very continuations that
    // start and fire the limit (BL-1062). On its own thread curl starts at once, and the limit
    // starts here, synchronously, without waiting for the pool.
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Whatever curl throws is reported as the case's failure.")]
    private async Task<(int ExitCode, string? Failure)> RunCurlAsync(UpstreamCurlInvocation invocation, SwsHttpServerConnector server)
    {
        Task<int> curlRun = Task.Factory.StartNew(() => runCurl(invocation), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        try
        {
            return (await curlRun.WaitAsync(timeLimit, timeProvider).ConfigureAwait(false), null);
        }
        catch (TimeoutException)
        {
            server.Abandon();
            return (0, $"curl did not finish within {timeLimit.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds");
        }
        catch (Exception exception)
        {
            return (0, $"curl threw {exception.GetType().Name}: {exception.Message}");
        }
    }

    // runtests.pl skips the case with the first line a precheck prints, or with "precheck command
    // error" when it prints nothing and exits non-zero. Screening let only interpreted one-liners through.
    private string? PrecheckSkipReason(UpstreamTestCase testCase) =>
        FirstFailedCheck(testCase, "client", "precheck", result => result.ExitCode != 0 || result.Output.Length > 0) is { } precheck
            ? precheck.Output.Length > 0 ? precheck.Output.Split('\n')[0].Replace("\r", "", StringComparison.Ordinal) : "precheck command error"
            : null;

    private UpstreamPerlOneLinerResult? FirstFailedCheck(UpstreamTestCase testCase, string section, string name, Func<UpstreamPerlOneLinerResult, bool> failed) =>
        UpstreamTestPartBodies.Lines(testCase.Find(section, name))
            .Select(line => UpstreamPerlCheckLine.RunLine(line, platform.OperatingSystemName) ?? UpstreamResolveCheck.RunLine(line)!)
            .FirstOrDefault(failed);

    private static List<string> Arguments(UpstreamTestCase testCase, string outputFile, string traceFile)
    {
        UpstreamTestSection command = testCase.Find("client", "command")!;
        string option = command.GetAttribute("option") ?? string.Empty;
        bool writesOutputFile = !option.Contains("no-output", StringComparison.Ordinal)
            && (testCase.Find("verify", "stdout") is null || option.Contains("force-output", StringComparison.Ordinal));
        List<string> arguments = writesOutputFile ? ["--output", outputFile] : [];
        arguments.AddRange(option.Contains("no-include", StringComparison.Ordinal) ? [] : ["--include"]);
        arguments.AddRange([option.Contains("binary-trace", StringComparison.Ordinal) ? "--trace" : "--trace-ascii", traceFile, "--trace-config", "all", "--trace-time"]);
        arguments.AddRange(UpstreamCommandLineSplitter.Split(UpstreamTestPartBodies.Text(command)).Arguments);
        return arguments;
    }

    private static void WriteClientFiles(UpstreamTestCase testCase)
    {
        foreach (UpstreamTestSection part in ClientFileParts.SelectMany(name => testCase.FindAll("client", name)))
        {
            string path = part.GetAttribute("name")!;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, ClientBytes(part));
        }
    }

    private static byte[] StandardInput(UpstreamTestCase testCase) =>
        testCase.Find("client", "stdin") is { } part
            ? ClientBytes(part)
            : [];

    // A <file> or <stdin> part as runtests.pl writes it: the last line chomped under nonewline,
    // then CRLF forced on every line under crlf="yes" or on header lines under crlf="headers".
    private static byte[] ClientBytes(UpstreamTestSection part) =>
        UpstreamTestPartBodies.WithCrlf(UpstreamTestPartBodies.WithoutFinalNewline(part.Content.ToArray(), part), part);
}
