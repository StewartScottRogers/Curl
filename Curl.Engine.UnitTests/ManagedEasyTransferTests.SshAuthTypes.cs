using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;

namespace Curl.Engine;

// CURLOPT_SSH_AUTH_TYPES reaches the SSH login (BL-5670).
public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    [DataRow(SshAuthenticationTypes.Password, CurlCode.Ok, DisplayName = "password only, a password server")]
    [DataRow(SshAuthenticationTypes.Keyboard, CurlCode.LoginDenied, DisplayName = "keyboard only, a password server")]
    public async Task PerformAsync_SshAuthenticationTypes_LimitTheLoginMethods(SshAuthenticationTypes types, CurlCode expected)
    {
        Original.Protocol.Ssh.Fakes.InMemorySshServer server = SshServerWithFile();
        await using ManagedCurlEngine engine = CreateEngine(server);
        await using IEasyTransfer easy = engine.CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new() { Url = "sftp://127.0.0.1:2222/f" },
            Authentication = new() { UserPassword = $"{SshUser}:{SshPassword}" },
            Protocols = new() { Ssh = new() { AuthenticationTypes = types } },
            Callbacks = new() { Write = data => data.Length },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(expected, code);
    }
}
