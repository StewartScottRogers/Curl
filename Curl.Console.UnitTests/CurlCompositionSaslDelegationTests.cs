using System.Text;

using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Runs SMTP's SASL GSSAPI through the production composition over a <see cref="ScriptedConnector" />
/// playing a server that offers only <c>AUTH GSSAPI</c>, and shows <c>--delegation</c> reaching the
/// Kerberos context request, as curl's GSS-API build passes <c>CURLOPT_GSSAPI_DELEGATION</c> to
/// <c>gss_init_sec_context</c> for SASL too (BL-874). The context refuses its first token, so the
/// transfer ends with exit 94 as curl's does without a ticket (BL-856).
/// </summary>
[TestClass]
public sealed class CurlCompositionSaslDelegationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(new string[0], SecurityDelegation.None, DisplayName = "no --delegation")]
    [DataRow(new[] { "--delegation", "policy" }, SecurityDelegation.Policy, DisplayName = "--delegation policy")]
    [DataRow(new[] { "--delegation", "always" }, SecurityDelegation.Always, DisplayName = "--delegation always")]
    public async Task RunAsync_ServerOffersOnlyGssapi_AsksTheKerberosContextForTheDelegationLevel(string[] delegationArguments, SecurityDelegation expected)
    {
        Diagnostics.Arrange("delegation arguments", string.Join(" ", delegationArguments));
        Diagnostics.Arrange("expected delegation", expected);
        RefusingContexts contexts = new();
        ScriptedConnector server = new([Encoding.ASCII.GetBytes("220 localhost ESMTP\r\n250-localhost\r\n250 AUTH GSSAPI\r\n"), Encoding.ASCII.GetBytes("334 \r\n")]);
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
                securityContexts: contexts)
            .RunAsync(["-sS", "-u", @"EXAMPLE\u:p", .. delegationArguments, "smtp://127.0.0.1:18025/"]);

        SecurityDelegation? requestedDelegation = contexts.Request?.Delegation;
        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Act("requested delegation", requestedDelegation);
        Diagnostics.Assert("exit code", (int)CurlExitCode.AuthError, exitCode);
        Diagnostics.Assert("requested delegation", expected, requestedDelegation);
        Assert.AreEqual((int)CurlExitCode.AuthError, exitCode);
        Assert.AreEqual(
            new SecurityContextRequest(SecurityMechanism.Kerberos, "smtp", "127.0.0.1")
            {
                UserName = "u",
                Password = "p",
                Domain = "EXAMPLE",
                MessageProtection = System.Net.Security.ProtectionLevel.Sign,
                Delegation = expected,
            },
            contexts.Request);
    }

    /// <summary>Records the request and hands out a context that cannot make its first token.</summary>
    private sealed class RefusingContexts : ISecurityContextFactory
    {
        public SecurityContextRequest? Request { get; private set; }

        public ISecurityContext Create(SecurityContextRequest request)
        {
            Request = request;
            return new RefusingContext();
        }
    }

    private sealed class RefusingContext : ISecurityContext
    {
        public bool IsCompleted => false;

        public ValueTask<SecurityContextStep> NextTokenAsync(ReadOnlyMemory<byte> incomingToken, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new SecurityContextStep(SecurityContextStatus.NoCredentials, []));

        public byte[]? Wrap(ReadOnlySpan<byte> message, bool encrypt) => throw new NotSupportedException();

        public byte[]? Unwrap(ReadOnlySpan<byte> wrappedMessage) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
