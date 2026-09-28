using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Negotiation;

[TestClass]
public sealed class SshAlgorithmNegotiatorTests
{
    private static readonly SshKexInit Client = SshServerScript.OpenSshKexInit(kexInit => kexInit with
    {
        KeyExchange = ["ext-info-c", "curve25519-sha256", "diffie-hellman-group14-sha256", "kex-strict-c-v00@openssh.com"],
        ServerHostKey = ["ssh-ed25519", "rsa-sha2-512"],
        CipherClientToServer = ["aes256-ctr", "aes128-ctr"],
        CipherServerToClient = ["aes128-ctr", "aes256-ctr"],
        MacClientToServer = ["hmac-sha2-512", "hmac-sha2-256"],
        MacServerToClient = ["hmac-sha2-256", "hmac-sha2-512"],
        CompressionClientToServer = ["zlib", "none"],
        CompressionServerToClient = ["none"],
    });

    private static readonly SshKexInit Server = SshServerScript.OpenSshKexInit(kexInit => kexInit with
    {
        KeyExchange = ["diffie-hellman-group14-sha256", "curve25519-sha256", "ext-info-c", "kex-strict-s-v00@openssh.com"],
        ServerHostKey = ["rsa-sha2-512", "ssh-ed25519"],
        CipherClientToServer = ["aes128-ctr", "aes256-ctr"],
        CipherServerToClient = ["aes256-ctr", "aes128-ctr"],
        MacClientToServer = ["hmac-sha2-256", "hmac-sha2-512"],
        MacServerToClient = ["hmac-sha2-512", "hmac-sha2-256"],
        CompressionClientToServer = ["none", "zlib"],
        CompressionServerToClient = ["zlib", "none"],
    });

    [TestMethod]
    public void Negotiate_PicksTheClientsFirstNameTheServerAlsoHas_EachDirectionApart()
    {
        SshNegotiatedAlgorithms? algorithms = SshAlgorithmNegotiator.Negotiate(Client, Server);

        Assert.IsNotNull(algorithms);
        Assert.AreEqual("curve25519-sha256", algorithms.KeyExchange, "ext-info-c is a signal, never a method");
        Assert.AreEqual("ssh-ed25519", algorithms.ServerHostKey);
        Assert.AreEqual("aes256-ctr", algorithms.CipherClientToServer);
        Assert.AreEqual("aes128-ctr", algorithms.CipherServerToClient);
        Assert.AreEqual("hmac-sha2-512", algorithms.MacClientToServer);
        Assert.AreEqual("hmac-sha2-256", algorithms.MacServerToClient);
        Assert.AreEqual("zlib", algorithms.CompressionClientToServer);
        Assert.AreEqual("none", algorithms.CompressionServerToClient);
        Assert.IsTrue(algorithms.IsStrictKeyExchange);
        Assert.IsFalse(algorithms.DiscardServerGuess);
    }

    [TestMethod]
    [DataRow("kex", DisplayName = "KexAlgorithms, measured: exit 2, -5")]
    [DataRow("hostkey", DisplayName = "HostKeyAlgorithms, measured: exit 2, -5")]
    [DataRow("cipher-out", DisplayName = "Ciphers, measured: exit 2, -5")]
    [DataRow("cipher-in")]
    [DataRow("mac-out", DisplayName = "MACs, measured: exit 2, -5")]
    [DataRow("mac-in")]
    [DataRow("compression-out")]
    [DataRow("compression-in")]
    public void Negotiate_OneListSharesNothing_ReturnsNull(string list)
    {
        string[] nothing = ["nothing-shared@example.com"];
        SshKexInit server = list switch
        {
            "kex" => Server with { KeyExchange = ["ext-info-c", "kex-strict-c-v00@openssh.com", .. nothing] },
            "hostkey" => Server with { ServerHostKey = nothing },
            "cipher-out" => Server with { CipherClientToServer = nothing },
            "cipher-in" => Server with { CipherServerToClient = nothing },
            "mac-out" => Server with { MacClientToServer = nothing },
            "mac-in" => Server with { MacServerToClient = nothing },
            "compression-out" => Server with { CompressionClientToServer = nothing },
            _ => Server with { CompressionServerToClient = nothing },
        };

        Assert.IsNull(SshAlgorithmNegotiator.Negotiate(Client, server));
    }

    [TestMethod]
    [DataRow("chacha20-poly1305@openssh.com")]
    [DataRow("aes256-gcm@openssh.com")]
    [DataRow("aes128-gcm@openssh.com")]
    public void Negotiate_AeadCipher_ConsultsNoMacList(string cipher)
    {
        SshKexInit client = Client with { CipherClientToServer = [cipher], CipherServerToClient = [cipher], MacClientToServer = [], MacServerToClient = [] };
        SshKexInit server = Server with { CipherClientToServer = [cipher], CipherServerToClient = [cipher] };

        SshNegotiatedAlgorithms? algorithms = SshAlgorithmNegotiator.Negotiate(client, server);

        Assert.IsNotNull(algorithms);
        Assert.IsNull(algorithms.MacClientToServer);
        Assert.IsNull(algorithms.MacServerToClient);
    }

    [TestMethod]
    public void Negotiate_ServerWithoutStrictSignal_IsNotStrict()
    {
        SshKexInit server = Server with { KeyExchange = ["curve25519-sha256"] };

        Assert.IsFalse(SshAlgorithmNegotiator.Negotiate(Client, server)!.IsStrictKeyExchange);
    }

    [TestMethod]
    public void Negotiate_ClientWithoutStrictSignal_IsNotStrict()
    {
        SshKexInit client = Client with { KeyExchange = ["curve25519-sha256"] };

        Assert.IsFalse(SshAlgorithmNegotiator.Negotiate(client, Server)!.IsStrictKeyExchange);
    }

    [TestMethod]
    [DataRow("curve25519-sha256", "ssh-ed25519", false, DisplayName = "right guess")]
    [DataRow("diffie-hellman-group14-sha256", "ssh-ed25519", true, DisplayName = "wrong key exchange")]
    [DataRow("curve25519-sha256", "rsa-sha2-512", true, DisplayName = "wrong host key")]
    public void Negotiate_ServerGuessFollows_DiscardsOnlyAWrongGuess(string guessedKeyExchange, string guessedHostKey, bool discard)
    {
        SshKexInit server = Server with
        {
            KeyExchange = [guessedKeyExchange, .. Server.KeyExchange],
            ServerHostKey = [guessedHostKey, .. Server.ServerHostKey],
            FirstKexPacketFollows = true,
        };

        Assert.AreEqual(discard, SshAlgorithmNegotiator.Negotiate(Client, server)!.DiscardServerGuess);
    }
}
