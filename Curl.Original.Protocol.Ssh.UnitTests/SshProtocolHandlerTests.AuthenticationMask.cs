using System.Net;
using Curl.Original.Protocol.Ssh.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ssh;

/// <summary>
/// Pins that <c>CURLOPT_SSH_AUTH_TYPES</c>, as <see cref="SshOptions.AuthenticationMask" />, limits the
/// login methods tried, as libcurl's libssh2 backend does (BL-5670).
/// </summary>
public sealed partial class SshProtocolHandlerTests
{
    private const uint MaskPublicKey = 1u << 0;

    private const uint MaskPassword = 1u << 1;

    private const uint MaskKeyboard = 1u << 3;

    private const uint MaskAgent = 1u << 4;

    private async Task<(Outcome Outcome, string[] Attempts)> RunWithMaskAsync(uint mask, string password)
    {
        InMemorySshServer server = new(User, Password) { OffersKeyboardInteractive = true, OffersPasswordToo = true };
        server.Files["/f"] = Hello;

        Outcome outcome = await RunAsync(
            server,
            $"sftp://{Host}/f",
            options: new SshOptions { AuthenticationMask = mask },
            credentials: new NetworkCredential(User, password));

        return (outcome, [.. server.Events.Where(e => e.StartsWith("auth ", StringComparison.Ordinal) && !e.StartsWith("auth none", StringComparison.Ordinal))]);
    }

    [TestMethod]
    public async Task ExecuteAsync_PasswordOnlyMaskAndAWrongPassword_SendsOnePasswordRequestAndIsExit67()
    {
        (Outcome outcome, string[] attempts) = await RunWithMaskAsync(MaskPassword, "wrong");

        AssertFailure(outcome, CurlExitCode.LoginDenied, "Authentication failure");
        CollectionAssert.AreEqual(new[] { $"auth password {User} refused" }, attempts.Where(a => !a.StartsWith("auth publickey", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    public async Task ExecuteAsync_KeyboardOnlyMask_SendsNoPasswordRequest()
    {
        (Outcome outcome, string[] attempts) = await RunWithMaskAsync(MaskKeyboard, Password);

        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        CollectionAssert.AreEqual(new[] { $"auth keyboard-interactive {User} ok" }, attempts);
    }

    [TestMethod]
    public async Task ExecuteAsync_DefaultMask_TriesThePasswordBeforeKeyboardInteractive()
    {
        (Outcome outcome, string[] attempts) = await RunWithMaskAsync(uint.MaxValue, Password);

        Assert.AreEqual(CurlExitCode.Ok, outcome.Result.ExitCode);
        CollectionAssert.AreEqual(new[] { $"auth password {User} ok" }, attempts.Where(a => !a.StartsWith("auth publickey", StringComparison.Ordinal)).ToArray());
    }

    [TestMethod]
    [DataRow(MaskPublicKey, DisplayName = "public key only")]
    [DataRow(MaskAgent, DisplayName = "agent only")]
    public async Task ExecuteAsync_MaskWithoutPasswordOrKeyboard_SendsNeitherAndIsExit67(uint mask)
    {
        (Outcome outcome, string[] attempts) = await RunWithMaskAsync(mask, Password);

        AssertFailure(outcome, CurlExitCode.LoginDenied, "Authentication failure");
        Assert.IsFalse(attempts.Any(a => a.StartsWith("auth password", StringComparison.Ordinal) || a.StartsWith("auth keyboard", StringComparison.Ordinal)));
    }
}
