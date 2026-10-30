using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins CGR-269 (BL-3798): <c>--pubkey</c> naming a PEM private key file decodes the first
/// line's <c>RSA</c> to two bytes, so libssh2 refuses it as too short and sends no
/// <c>publickey</c> request, as curl 8.22.0 does.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    [TestMethod]
    public async Task AuthenticateAsync_PubkeyIsAPemPrivateKey_SendsNoPublicKeyRequest()
    {
        SshUserKeySource keys = new(
            new InMemoryKeyFileSystem(new Dictionary<string, string> { [KeyPath] = TestUserKeys.RsaPkcs1, [PublicKeyPath] = TestUserKeys.RsaPkcs1 }),
            _ => null,
            new SshOptions { PrivateKeyPath = KeyPath, PublicKeyPath = PublicKeyPath },
            Encoding.UTF8);
        KeyedPeer peer = await ConnectAsync(keys, Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
    }
}
