using System.Net;
using System.Security.Cryptography;
using System.Text;
using Curl.Cryptography;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.Keys;
using Curl.Protocol.Ssh.Negotiation;
using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.Authentication;

/// <summary>
/// Pins <c>publickey</c> authentication (BL-568, ADR-0230) against an in-memory peer after a
/// real key exchange, so the signature covers a known session identifier: the question
/// without a signature, the signed request, the algorithm chosen from
/// <c>server-sig-algs</c>, and each outcome measured 2026-09-29 with curl 8.21.0 (libssh2
/// 1.11.1, WinCNG) against a loopback server built from this library. The user's password
/// is wrong throughout, as it was in the measurement, so a failed <c>publickey</c> shows as
/// curl going on to <c>password</c>.
/// </summary>
public sealed partial class SshUserAuthenticationTests
{
    private const string KeyPath = "/keys/id";

    private const string PublicKeyPath = "/keys/id.pub";

    private const string MeasuredSignatureAlgorithms = "rsa-sha2-512,rsa-sha2-256,ssh-rsa,ecdsa-sha2-nistp256,ecdsa-sha2-nistp384,ssh-ed25519,ssh-dss";

    private static readonly NetworkCredential WrongPassword = new("tester", "wrong");

    private static readonly byte[] RsaBlob = SshPublicKeyFile.Parse(TestUserKeys.RsaPublicKeyFile)!.Blob;

    private static readonly byte[] EcdsaBlob = SshPublicKeyFile.Parse(TestUserKeys.EcdsaP256PublicKeyFile)!.Blob;

    [TestMethod]
    [DataRow(MeasuredSignatureAlgorithms, "rsa-sha2-512", DisplayName = "OpenSSH's list: rsa-sha2-512, as measured")]
    [DataRow("rsa-sha2-256", "rsa-sha2-256", DisplayName = "rsa-sha2-256 alone, as measured")]
    [DataRow("rsa-sha2-256,rsa-sha2-512", "rsa-sha2-512", DisplayName = "libssh2's order, not the server's, as measured")]
    [DataRow("ssh-rsa", "ssh-rsa", DisplayName = "ssh-rsa alone, as measured")]
    [DataRow(null, "ssh-rsa", DisplayName = "no EXT_INFO: ssh-rsa, as measured")]
    public async Task AuthenticateAsync_RsaKeyAccepted_AsksThenSignsWithTheAlgorithmServerSigAlgsAllowsAsMeasured(string? serverSignatureAlgorithms, string algorithm)
    {
        byte[][] extensionInfo = serverSignatureAlgorithms is null ? [] : [ExtensionInfo(("server-sig-algs", serverSignatureAlgorithms))];
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), [.. extensionInfo, Failure("publickey,password"), PublicKeyOk(algorithm, RsaBlob), Success]);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        Assert.HasCount(3, written);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", algorithm, RsaBlob, signed: false), written[1]);
        AssertSigned(peer, written[2], algorithm, RsaBlob);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ServerSigAlgsNamesNoRsaAlgorithm_SkipsPublicKeyAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), ExtensionInfo(("server-sig-algs", "ssh-ed25519")), Failure("publickey,password"), Failure("publickey,password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyNotAuthorized_GoesOnToThePasswordAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)), Failure("publickey,password"), Failure("publickey,password"), Failure("publickey,password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "publickey", "password");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-512", RsaBlob, signed: false), written[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_KeyNotAuthorizedAndKeyboardInteractiveListed_LoginDeniedAsMeasured()
    {
        const string Methods = "publickey,password,keyboard-interactive";
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure(Methods), Failure(Methods), Failure(Methods), Failure(Methods));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertLoginDenied(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey", "password", "keyboard-interactive");
    }

    [TestMethod]
    public async Task AuthenticateAsync_SignatureRefused_GoesOnToThePasswordAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Failure("publickey,password"), Failure("publickey,password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey", "publickey", "password");
    }

    [TestMethod]
    public async Task AuthenticateAsync_QuestionAnsweredWithSuccess_AuthenticatesWithoutSigningAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey,password"), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey");
    }

    [TestMethod]
    public async Task AuthenticateAsync_PeerClosesAfterTheQuestion_FailsThePublicKeyMethod()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey");
    }

    [TestMethod]
    public async Task AuthenticateAsync_PeerClosesAfterTheSignedRequest_FailsThePublicKeyMethod()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), Failure("publickey"), PublicKeyOk("ssh-rsa", RsaBlob));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "publickey", "publickey");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "missing --key file, as measured")]
    [DataRow(TestUserKeys.RsaPkcs1Aes128, DisplayName = "encrypted key without --pass, as measured")]
    [DataRow(TestUserKeys.Ed25519OpenSsh, DisplayName = "a key type not read here")]
    public async Task AuthenticateAsync_PrivateKeyUnreadableAndNoPubkey_SendsNoPublicKeyRequestAsMeasured(string? privateKeyText)
    {
        KeyedPeer peer = await ConnectAsync(Keys(privateKeyText), Failure("publickey,password"), Failure("publickey,password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
    }

    [TestMethod]
    public async Task AuthenticateAsync_EncryptedKeyWithItsPassphrase_AuthenticatesAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1Aes128, passphrase: TestUserKeys.Passphrase), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertSigned(peer, written[2], "ssh-rsa", RsaBlob);
    }

    [TestMethod]
    public async Task AuthenticateAsync_PubkeyGiven_SendsTheFilesKeyAndSignsWithThePrivateKeyAsMeasured()
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaOpenSsh, TestUserKeys.RsaPublicKeyFile), Failure("publickey,password"), PublicKeyOk("ssh-rsa", RsaBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: false), written[1]);
        AssertSigned(peer, written[2], "ssh-rsa", RsaBlob);
    }

    [TestMethod]
    [DataRow(TestUserKeys.RsaPkcs1, DisplayName = "an RSA private key, as measured")]
    [DataRow(null, DisplayName = "a missing private key, as measured")]
    public async Task AuthenticateAsync_PubkeyTheServerAcceptsButNoMatchingPrivateKey_AsksThenGoesOnToThePasswordAsMeasured(string? privateKeyText)
    {
        KeyedPeer peer = await ConnectAsync(
            Keys(privateKeyText, TestUserKeys.EcdsaP256PublicKeyFile),
            ExtensionInfo(("server-sig-algs", "rsa-sha2-512")),
            Failure("publickey,password"),
            PublicKeyOk("ecdsa-sha2-nistp256", EcdsaBlob),
            Failure("publickey,password"));

        SshTransferException failure = await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertAuthenticationFailure(failure);
        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertMethods(written, "none", "publickey", "password");
        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ecdsa-sha2-nistp256", EcdsaBlob, signed: false), written[1], "an ECDSA key is not narrowed by server-sig-algs");
    }

    [TestMethod]
    [DataRow(null, DisplayName = "missing --pubkey file, as measured")]
    [DataRow("ssh-rsa\n", DisplayName = "no base64")]
    public async Task AuthenticateAsync_PubkeyUnreadable_SendsNoPublicKeyRequestAsMeasured(string? publicKeyText)
    {
        SshUserKeySource keys = new(
            new InMemoryKeyFileSystem(publicKeyText is null ? new Dictionary<string, string> { [KeyPath] = TestUserKeys.RsaPkcs1 } : new Dictionary<string, string> { [KeyPath] = TestUserKeys.RsaPkcs1, [PublicKeyPath] = publicKeyText }),
            _ => null,
            new SshOptions { PrivateKeyPath = KeyPath, PublicKeyPath = PublicKeyPath },
            Encoding.UTF8);
        KeyedPeer peer = await ConnectAsync(keys, Failure("publickey,password"), Failure("publickey,password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
    }

    [TestMethod]
    public async Task AuthenticateAsync_PublicKeyNotListed_SendsNoPublicKeyRequestAsMeasured()
    {
        InMemoryKeyFileSystem files = new(new Dictionary<string, string> { [KeyPath] = TestUserKeys.RsaPkcs1 });
        KeyedPeer peer = await ConnectAsync(new SshUserKeySource(files, _ => null, new SshOptions { PrivateKeyPath = KeyPath }, Encoding.UTF8), Failure("password"), Failure("password"));

        await Assert.ThrowsExactlyAsync<SshTransferException>(
            async () => await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None));

        AssertMethods(await AuthenticationMessagesAsync(peer), "none", "password");
        Assert.IsEmpty(files.Opened, "the key files are not even looked for");
    }

    [TestMethod]
    [DataRow(TestUserKeys.EcdsaP256Sec1, DisplayName = "P-256")]
    [DataRow(TestUserKeys.EcdsaP384Sec1, DisplayName = "P-384")]
    [DataRow(TestUserKeys.EcdsaP521OpenSsh, DisplayName = "P-521")]
    public async Task AuthenticateAsync_EcdsaKey_SignsAsItsKeyType(string privateKeyText)
    {
        SshPublicKey publicKey = SshPrivateKeyReader.Read(privateKeyText, [])!.PublicKey;
        KeyedPeer peer = await ConnectAsync(Keys(privateKeyText), ExtensionInfo(("server-sig-algs", MeasuredSignatureAlgorithms)), Failure("publickey"), PublicKeyOk(publicKey.KeyType, publicKey.Blob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        CollectionAssert.AreEqual(PublicKeyRequest("tester", publicKey.KeyType, publicKey.Blob, signed: false), written[1]);
        AssertSigned(peer, written[2], publicKey.KeyType, publicKey.Blob);
    }

    [TestMethod]
    public async Task AuthenticateAsync_DsaKey_SignsAsSshDss()
    {
        string text = "-----BEGIN DSA PRIVATE KEY-----\n" + Convert.ToBase64String(TestDsaKey.Traditional()) + "\n-----END DSA PRIVATE KEY-----\n";
        KeyedPeer peer = await ConnectAsync(Keys(text), Failure("publickey"), PublicKeyOk("ssh-dss", TestDsaKey.PublicKeyBlob), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        List<byte[]> written = await AuthenticationMessagesAsync(peer);
        AssertSigned(peer, written[2], "ssh-dss", TestDsaKey.PublicKeyBlob);
    }

    [TestMethod]
    [DataRow("03", DisplayName = "shorter than five bytes")]
    [DataRow("00000002", DisplayName = "a pair cut short")]
    [DataRow("000000010000000F7365727665722D7369672D616C6773", DisplayName = "a value missing")]
    public async Task AuthenticateAsync_MalformedExtensionInfo_KeepsNoServerSigAlgs(string bodyHex)
    {
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), [SshMessageNumber.ExtensionInfo, .. Convert.FromHexString(bodyHex)], Failure("publickey"), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        CollectionAssert.AreEqual(PublicKeyRequest("tester", "ssh-rsa", RsaBlob, signed: false), (await AuthenticationMessagesAsync(peer))[1]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_SeveralExtensions_KeepsTheLastServerSigAlgs()
    {
        byte[] extensionInfo = ExtensionInfo(("server-sig-algs", "ssh-rsa"), ("delay-compression", "none"), ("server-sig-algs", "rsa-sha2-256"));
        KeyedPeer peer = await ConnectAsync(Keys(TestUserKeys.RsaPkcs1), extensionInfo, Failure("publickey"), Success);

        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-256", RsaBlob, signed: false), (await AuthenticationMessagesAsync(peer))[1]);
    }

    [TestMethod]
    public async Task RequestServiceAsync_ExtensionInfoBeforeTheAcceptance_KeepsServerSigAlgsForPublicKey()
    {
        KeyedPeer peer = await ConnectAsync(
            Keys(TestUserKeys.RsaPkcs1),
            ExtensionInfo(("server-sig-algs", "rsa-sha2-256")),
            [SshMessageNumber.ServiceAccept, .. Name("ssh-userauth")],
            Failure("publickey"),
            Success);

        await peer.Authentication.RequestServiceAsync(CancellationToken.None);
        await peer.Authentication.AuthenticateAsync(WrongPassword, CancellationToken.None);

        CollectionAssert.AreEqual(PublicKeyRequest("tester", "rsa-sha2-256", RsaBlob, signed: false), (await AuthenticationMessagesAsync(peer))[2]);
    }

    private static SshUserKeySource Keys(string? privateKeyText, string? publicKeyText = null, string? passphrase = null)
    {
        Dictionary<string, string> files = [];
        if (privateKeyText is not null)
        {
            files[KeyPath] = privateKeyText;
        }

        if (publicKeyText is not null)
        {
            files[PublicKeyPath] = publicKeyText;
        }

        SshOptions options = new() { PrivateKeyPath = KeyPath, PublicKeyPath = publicKeyText is null ? null : PublicKeyPath, PrivateKeyPassphrase = passphrase };
        return new SshUserKeySource(new InMemoryKeyFileSystem(files), _ => null, options, Encoding.UTF8);
    }

    private static byte[] ExtensionInfo(params (string Name, string Value)[] extensions) =>
        Join([[SshMessageNumber.ExtensionInfo], UInt32((uint)extensions.Length), .. extensions.Select(extension => Join(Name(extension.Name), Name(extension.Value)))]);

    private static byte[] PublicKeyOk(string algorithm, byte[] blob) => Join([SshAuthenticationMessageNumber.PublicKeyOk], Name(algorithm), String(blob));

    private static byte[] PublicKeyRequest(string user, string algorithm, byte[] blob, bool signed) =>
        Join([SshAuthenticationMessageNumber.Request], Utf8(user), Name("ssh-connection"), Name("publickey"), [signed ? (byte)1 : (byte)0], Name(algorithm), String(blob));

    // After a key exchange with the scripted server the client's messages are sealed, so the
    // transcript opens them with the client-to-server keys.
    private static async Task<KeyedPeer> ConnectAsync(SshUserKeySource keys, params byte[][] payloads)
    {
        TestHostKey hostKey = TestHostKey.Ecdsa("nistp256", TestHostKey.FixedNistP256);
        SshKexInit serverKexInit = ServerKexInit("ecdh-sha2-nistp256", hostKey.Algorithm);
        TestEphemeralKeys ephemeralKeys = new();
        TestKeyExchangeServer exchange = TestKeyExchangeServer.Answer("ecdh-sha2-nistp256", hostKey, ephemeralKeys, ClientKexInit, serverKexInit.ToPayload());
        SshNegotiatedAlgorithms ctr = SshTestAlgorithms.With("aes128-ctr", "hmac-sha2-256");
        SshServerScript script = new SshServerScript().Line(TestKeyExchangeServer.ServerIdentification).KexInit(serverKexInit);
        exchange.ServerPayloads.ForEach(payload => script.Packet(payload));
        script.Packet(SshMessageNumber.NewKeys).Protect(SshPacketProtections.ForServerToClient(ctr, exchange.Keys(exchange.ExchangeHash)), resetSequenceNumber: false);
        foreach (byte[] payload in payloads)
        {
            script.Packet(payload);
        }

        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, EverythingImplemented, new RepeatingRandomSource(0x33), ephemeralKeys);
        await transport.ExchangeKeysAsync(await transport.NegotiateAlgorithmsAsync(CancellationToken.None), CancellationToken.None);
        TranscriptTransferEvents events = new();
        return new KeyedPeer(new SshUserAuthentication(transport, Encoding.UTF8, keys, events), connection, exchange.ExchangeHash, SshPacketProtections.ForClientToServer(ctr, exchange.Keys(exchange.ExchangeHash)), events);
    }

    // The client's messages after its KEXINIT, key-exchange message and NEWKEYS.
    private static async Task<List<byte[]>> AuthenticationMessagesAsync(KeyedPeer peer) =>
        [.. (await SshClientTranscript.PayloadsAsync(peer.Connection.Written, false, peer.ClientProtection)).Skip(3)];

    private static void AssertMethods(List<byte[]> written, params string[] methods)
    {
        Assert.HasCount(methods.Length, written);
        for (int index = 0; index < methods.Length; index++)
        {
            SshWireReader reader = new(written[index].AsMemory(1));
            reader.ReadString();
            reader.ReadString();
            Assert.AreEqual(methods[index], reader.ReadName(), $"client message {index}");
        }
    }

    // RFC 4252 section 7: the request with the flag set, then a signature blob naming the
    // algorithm, over the session identifier as a string and the request up to the blob.
    private static void AssertSigned(KeyedPeer peer, byte[] message, string algorithm, byte[] publicKeyBlob)
    {
        byte[] signedPart = PublicKeyRequest("tester", algorithm, publicKeyBlob, signed: true);
        CollectionAssert.AreEqual(signedPart, message[..signedPart.Length]);
        SshWireReader signatureBlob = new(new SshWireReader(message.AsMemory(signedPart.Length)).ReadString());
        Assert.AreEqual(algorithm, signatureBlob.ReadName());
        byte[] signature = signatureBlob.ReadString().ToArray();
        Assert.IsTrue(Verify(publicKeyBlob, algorithm, Join(String(peer.SessionIdentifier), signedPart), signature), "the signature verifies against the public key");
    }

    private static bool Verify(byte[] publicKeyBlob, string algorithm, byte[] data, byte[] signature)
    {
        SshWireReader key = new(publicKeyBlob);
        string keyType = key.ReadName();
        if (keyType == "ssh-rsa")
        {
            byte[] exponent = key.ReadMpint().ToArray();
            using RSA rsa = RSA.Create(new RSAParameters { Exponent = exponent, Modulus = key.ReadMpint().ToArray() });
            HashAlgorithmName hash = algorithm switch
            {
                "rsa-sha2-512" => HashAlgorithmName.SHA512,
                "rsa-sha2-256" => HashAlgorithmName.SHA256,
                _ => HashAlgorithmName.SHA1,
            };
            return rsa.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1);
        }

        if (keyType == "ssh-dss")
        {
            return DsaSignature.VerifyHash(TestDsaKey.Prime, TestDsaKey.Subprime, TestDsaKey.Generator, TestDsaKey.PublicKey, SHA1.HashData(data), signature);
        }

        return VerifyEcdsa(key, data, signature);
    }

    private static bool VerifyEcdsa(SshWireReader key, byte[] data, byte[] signature)
    {
        (ECCurve curve, HashAlgorithmName hash, int size) = key.ReadName() switch
        {
            "nistp256" => (ECCurve.NamedCurves.nistP256, HashAlgorithmName.SHA256, 32),
            "nistp384" => (ECCurve.NamedCurves.nistP384, HashAlgorithmName.SHA384, 48),
            _ => (ECCurve.NamedCurves.nistP521, HashAlgorithmName.SHA512, 66),
        };
        byte[] point = key.ReadString().ToArray();
        using ECDsa ecdsa = ECDsa.Create(new ECParameters { Curve = curve, Q = new ECPoint { X = point[1..(1 + size)], Y = point[(1 + size)..] } });
        SshWireReader values = new(signature);
        byte[] r = values.ReadMpint().ToArray();
        byte[] s = values.ReadMpint().ToArray();
        byte[] ieee = new byte[2 * size];
        r.CopyTo(ieee, size - r.Length);
        s.CopyTo(ieee, (2 * size) - s.Length);
        return ecdsa.VerifyData(data, ieee, hash);
    }

    private sealed record KeyedPeer(SshUserAuthentication Authentication, ScriptedConnection Connection, byte[] SessionIdentifier, ISshPacketProtection ClientProtection, TranscriptTransferEvents Events);
}
