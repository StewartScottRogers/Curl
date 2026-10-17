namespace Curl.Conformance;

/// <summary>What one run of a test case produced, for <see cref="UpstreamCaseVerification"/>.</summary>
/// <param name="exitCode">curl's exit code.</param>
/// <param name="standardOutput">Every byte written to standard output.</param>
/// <param name="standardError">Every byte written to standard error.</param>
/// <param name="receivedBytes">Every byte the emulated server received.</param>
/// <param name="replyDataBytes">What <c>&lt;reply&gt;&lt;data&gt;</c> is compared with: the <c>--output</c> file the harness added (empty when it was not written), or a libtest program's stdout.</param>
internal sealed class UpstreamCaseRun(int exitCode, byte[] standardOutput, byte[] standardError, byte[] receivedBytes, byte[] replyDataBytes)
{
    /// <summary>curl's exit code.</summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>Every byte written to standard output.</summary>
    public byte[] StandardOutput { get; } = standardOutput;

    /// <summary>Every byte written to standard error.</summary>
    public byte[] StandardError { get; } = standardError;

    /// <summary>Every byte the emulated server received.</summary>
    public byte[] ReceivedBytes { get; } = receivedBytes;

    /// <summary>Every byte the emulated http-proxy server on <c>%PROXYPORT</c> received, for <c>&lt;verify&gt;&lt;proxy&gt;</c>.</summary>
    public byte[] ProxyReceivedBytes { get; init; } = [];

    /// <summary>The last message the emulated SMTP server on <c>%SMTPPORT</c> received, for <c>&lt;verify&gt;&lt;upload&gt;</c>.</summary>
    public byte[] UploadedBytes { get; init; } = [];

    /// <summary>What the emulated socksd on <c>%SOCKSPORT</c> wrote to its dump file, for <c>&lt;verify&gt;&lt;socks&gt;</c>.</summary>
    public byte[] SocksLog { get; init; } = [];

    /// <summary>What the emulated dnsd on <c>%DNSPORT</c> wrote to <c>dnsd.input</c>, one <c>QNAME … QTYPE …</c> line per query, for <c>&lt;verify&gt;&lt;dns&gt;</c>.</summary>
    public string DnsLog { get; init; } = string.Empty;

    /// <summary>
    /// The case's <c>%LOGDIR</c>, or <see langword="null"/> when unknown: an expected <c>&lt;stderr&gt;</c>
    /// naming it is compared with curl's wrapped <c>Note:</c> and <c>Warning:</c> lines joined
    /// (<see cref="UpstreamWrappedMessageLines"/>).
    /// </summary>
    public string? LogDirectory { get; init; }

    /// <summary>
    /// What <c>&lt;reply&gt;&lt;data&gt;</c> is compared with, as <c>runtests.pl</c> 8.22.0 reads its
    /// <c>$CURLOUT</c>: the <c>--output</c> file the harness added for a curl run (empty when it was not
    /// written), or a libtest program's stdout, which <c>runner.pm</c> sets <c>$CURLOUT</c> to for a tool run.
    /// </summary>
    public byte[] ReplyDataBytes { get; } = replyDataBytes;

    /// <summary>What <see cref="ReplyDataBytes"/> holds, as a difference names it: <c>the --output file</c> unless set.</summary>
    public string ReplyDataName { get; init; } = "the --output file";
}
