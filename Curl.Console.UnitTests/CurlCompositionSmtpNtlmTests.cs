using System.Text;

using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Runs SMTP's SASL NTLM through the production composition over a <see cref="ScriptedConnector" />
/// playing a server that offers only <c>AUTH NTLM</c>, as curl 8.21.0 (Schannel) ran it on
/// 2026-09-29 against <c>Record-CurlExchange.ps1 -Smtp</c> (BL-538 Notes): <c>AUTH NTLM</c>, a bare
/// <c>334 </c>, the Type 1 message, <c>334</c> with the recorder's fixed Type 2, the Type 3 message,
/// <c>235</c>, then the mail transaction. The scripted token source proves
/// <see cref="CurlComposition.CreateProtocolHandlers" /> hands the run's security context factory to
/// the SASL authenticator (BL-852); the production router is run too, each platform pinned to its own
/// NTLM.
/// </summary>
[TestClass]
public sealed class CurlCompositionSmtpNtlmTests
{
    private const string Message = "Subject: t\r\n\r\nhello\r\n";

    private const string Type2 = "TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA";

    private const string Transaction =
        "MAIL FROM:<a@b>\r\nRCPT TO:<c@d>\r\nDATA\r\n" + Message + ".\r\nQUIT\r\n";

    [TestMethod]
    public async Task RunAsync_ServerOffersOnlyNtlm_AnswersWithTheRunsSecurityContexts()
    {
        TokenSource tokens = new();
        ScriptedConnector server = Server();

        (int exitCode, string[] lines) = await RunMailUploadAsync(server, tokens);

        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(new[] { "EHLO mail.txt", "AUTH NTLM", "AQ==", "Aw==", "MAIL FROM:<a@b>" }, lines[..5]);
        CollectionAssert.AreEqual(Convert.FromBase64String(Type2), tokens.Context!.IncomingTokens[1]);
        Assert.AreEqual(new SecurityContextRequest(SecurityMechanism.Ntlm, "smtp", "127.0.0.1") { UserName = "u", Password = "p" }, tokens.Request);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task RunAsync_WindowsProductionRoute_SendsSspisType1AndAType3AsCurl8210Measured()
    {
        ScriptedConnector server = Server();

        (int exitCode, string[] lines) = await RunMailUploadAsync(server, null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("AUTH NTLM", lines[1]);
        StringAssert.StartsWith(lines[2], "TlRMTVNTUAABAAAAB4IIog");
        StringAssert.StartsWith(lines[3], "TlRMTVNTUAADAAAA");
        Assert.AreEqual("MAIL FROM:<a@b>", lines[4]);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public async Task RunAsync_ProductionRouteOffWindows_SendsCurlsOwnType1AndAType3()
    {
        ScriptedConnector server = Server();

        (int exitCode, string[] lines) = await RunMailUploadAsync(server, null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("AUTH NTLM", lines[1]);
        Assert.AreEqual("TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=", lines[2]);
        StringAssert.StartsWith(lines[3], "TlRMTVNTUAADAAAA");
        Assert.AreEqual("MAIL FROM:<a@b>", lines[4]);
    }

    private static ScriptedConnector Server() =>
        new(
            [
                Encoding.ASCII.GetBytes("220 localhost ESMTP\r\n250-localhost\r\n250 AUTH NTLM\r\n"),
                Encoding.ASCII.GetBytes("334 \r\n"),
                Encoding.ASCII.GetBytes("334 " + Type2 + "\r\n"),
                Encoding.ASCII.GetBytes("235 Authentication successful\r\n"),
                Encoding.ASCII.GetBytes("250 OK\r\n250 OK\r\n354 End data with <CR><LF>.<CR><LF>\r\n250 OK message accepted\r\n221 Bye\r\n"),
            ]);

    private static async Task<(int ExitCode, string[] Lines)> RunMailUploadAsync(ScriptedConnector server, ISecurityContextFactory? tokens)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"curl-bl852-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string mail = Path.Combine(directory, "mail.txt");
        await System.IO.File.WriteAllBytesAsync(mail, Encoding.ASCII.GetBytes(Message));

        try
        {
            using MemoryStream standardOutput = new();
            using MemoryStream standardError = new();
            using MemoryStream standardInput = new();

            int exitCode = await CurlComposition
                .CreateRunner(
                    standardOutput,
                    standardError,
                    standardInput,
                    server,
                    new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                    securityContexts: tokens)
                .RunAsync(["-sS", "-u", "u:p", "--mail-from", "a@b", "--mail-rcpt", "c@d", "-T", mail, "smtp://127.0.0.1:18025/"]);

            return (exitCode, Encoding.ASCII.GetString(server.Written).Split("\r\n"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Hands out one context whose steps are Type 1 (<c>AQ==</c>) then Type 3 (<c>Aw==</c>), and
    /// records the request and the context.
    /// </summary>
    private sealed class TokenSource : ISecurityContextFactory
    {
        public SecurityContextRequest? Request { get; private set; }

        public TwoStepContext? Context { get; private set; }

        public ISecurityContext Create(SecurityContextRequest request)
        {
            Request = request;
            Context = new TwoStepContext();
            return Context;
        }
    }

    private sealed class TwoStepContext : ISecurityContext
    {
        public List<byte[]> IncomingTokens { get; } = [];

        public bool IsCompleted => IncomingTokens.Count == 2;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken)
        {
            IncomingTokens.Add(incomingToken.ToArray());
            return ValueTask.FromResult(IncomingTokens.Count == 1
                ? new SecurityContextStep(SecurityContextStatus.ContinueNeeded, [0x01])
                : new SecurityContextStep(SecurityContextStatus.Completed, [0x03]));
        }

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
