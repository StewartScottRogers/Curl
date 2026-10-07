using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Negotiation;

[TestClass]
public sealed class SshKexInitTests
{
    private static readonly SshAlgorithmCatalogue EverythingImplemented = new(
        SshAlgorithmPreferences.Full.KeyExchange
            .Concat(SshAlgorithmPreferences.Full.ServerHostKey)
            .Concat(SshAlgorithmPreferences.Full.Cipher)
            .Concat(SshAlgorithmPreferences.Full.Mac)
            .Concat(["zlib", "zlib@openssh.com", "none"]));

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ForClient_EverythingImplemented_OffersThePresetBothWaysWithNoLanguagesAndNoGuess()
    {
        SshAlgorithmPreferences preset = SshAlgorithmPreferences.WindowsReference;

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("preset", "WindowsReference");
        diagnostics.Arrange("catalogue", "everything implemented");
        diagnostics.Arrange("random byte", "0x42");

        SshKexInit kexInit = SshKexInit.ForClient(preset, EverythingImplemented, new RepeatingRandomSource(0x42));

        diagnostics.ActKexInit("client", kexInit);
        diagnostics.Diff("cookie", Enumerable.Repeat((byte)0x42, 16).ToArray(), kexInit.Cookie);
        diagnostics.DiffList("kex", string.Join(',', preset.KeyExchange), kexInit.KeyExchange);
        diagnostics.DiffList("host key", string.Join(',', preset.ServerHostKey), kexInit.ServerHostKey);
        diagnostics.DiffList("cipher c2s", string.Join(',', preset.Cipher), kexInit.CipherClientToServer);
        diagnostics.DiffList("mac c2s", string.Join(',', preset.Mac), kexInit.MacClientToServer);
        diagnostics.DiffList("compression c2s", "none", kexInit.CompressionClientToServer);
        diagnostics.Assert("first kex packet follows", false, kexInit.FirstKexPacketFollows);
        CollectionAssert.AreEqual(Enumerable.Repeat((byte)0x42, 16).ToArray(), kexInit.Cookie);
        CollectionAssert.AreEqual(preset.KeyExchange.ToArray(), kexInit.KeyExchange.ToArray());
        CollectionAssert.AreEqual(preset.ServerHostKey.ToArray(), kexInit.ServerHostKey.ToArray());
        CollectionAssert.AreEqual(preset.Cipher.ToArray(), kexInit.CipherClientToServer.ToArray());
        CollectionAssert.AreEqual(preset.Cipher.ToArray(), kexInit.CipherServerToClient.ToArray());
        CollectionAssert.AreEqual(preset.Mac.ToArray(), kexInit.MacClientToServer.ToArray());
        CollectionAssert.AreEqual(preset.Mac.ToArray(), kexInit.MacServerToClient.ToArray());
        CollectionAssert.AreEqual(new[] { "none" }, kexInit.CompressionClientToServer.ToArray());
        CollectionAssert.AreEqual(new[] { "none" }, kexInit.CompressionServerToClient.ToArray());
        Assert.AreEqual(0, kexInit.LanguagesClientToServer.Count);
        Assert.AreEqual(0, kexInit.LanguagesServerToClient.Count);
        Assert.IsFalse(kexInit.FirstKexPacketFollows);
    }

    [TestMethod]
    public void ToPayload_WindowsPreset_IsAsLongAsTheMeasuredKexInit()
    {
        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.WindowsReference, EverythingImplemented, new RepeatingRandomSource(0));

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeKexInit("client", kexInit);

        byte[] payload = kexInit.ToPayload();

        diagnostics.Act("message", SshAuthenticationDiagnostics.MessageName(payload));
        diagnostics.Bytes("payload", payload);
        diagnostics.Assert("payload length", 1072, payload.Length);
        diagnostics.Assert("message number", 20, payload[0]);
        diagnostics.Diff("last five bytes", new byte[] { 0, 0, 0, 0, 0 }, payload[^5..]);
        Assert.AreEqual(1072, payload.Length, "the reference build's KEXINIT payload measured 1072 bytes");
        Assert.AreEqual((byte)20, payload[0]);
        CollectionAssert.AreEqual(new byte[] { 0, 0, 0, 0, 0 }, payload[^5..], "first_kex_packet_follows false, reserved 0");
    }

    [TestMethod]
    public void ForClient_TodaysCatalogue_OffersCurve25519NistAndFiniteFieldExchangesEveryMeasuredHostKeyCipherAndMac()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("preset", "OpenSslReference");
        diagnostics.Arrange("catalogue", "implemented today");

        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.OpenSslReference, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0));

        diagnostics.ActKexInit("client", kexInit);
        diagnostics.DiffList("host key", string.Join(',', SshAlgorithmPreferences.OpenSslReference.ServerHostKey), kexInit.ServerHostKey);
        diagnostics.DiffList("compression c2s", "none", kexInit.CompressionClientToServer);
        CollectionAssert.AreEqual(
            new[]
            {
                "curve25519-sha256", "curve25519-sha256@libssh.org", "ecdh-sha2-nistp256", "ecdh-sha2-nistp384", "ecdh-sha2-nistp521",
                "diffie-hellman-group-exchange-sha256",
                "diffie-hellman-group16-sha512", "diffie-hellman-group18-sha512", "diffie-hellman-group14-sha256",
                "diffie-hellman-group14-sha1", "diffie-hellman-group1-sha1", "diffie-hellman-group-exchange-sha1",
                "ext-info-c", "kex-strict-c-v00@openssh.com",
            },
            kexInit.KeyExchange.ToArray());
        CollectionAssert.AreEqual(
            SshAlgorithmPreferences.OpenSslReference.ServerHostKey.ToArray(),
            kexInit.ServerHostKey.ToArray());
        string[] ciphers = "chacha20-poly1305@openssh.com,aes256-gcm@openssh.com,aes128-gcm@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,blowfish-cbc,arcfour128,arcfour,cast128-cbc,3des-cbc".Split(',');
        CollectionAssert.AreEqual(ciphers, kexInit.CipherClientToServer.ToArray());
        CollectionAssert.AreEqual(ciphers, kexInit.CipherServerToClient.ToArray());
        string[] macs = "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96,hmac-ripemd160,hmac-ripemd160@openssh.com".Split(',');
        CollectionAssert.AreEqual(macs, kexInit.MacClientToServer.ToArray());
        CollectionAssert.AreEqual(macs, kexInit.MacServerToClient.ToArray());
        CollectionAssert.AreEqual(new[] { "none" }, kexInit.CompressionClientToServer.ToArray());
    }

    [TestMethod]
    public void ForClient_TodaysCatalogueOnWindows_OffersTheMeasuredWinCngCiphersAndMacs()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("preset", "WindowsReference");
        diagnostics.Arrange("catalogue", "implemented today");

        SshKexInit kexInit = SshKexInit.ForClient(SshAlgorithmPreferences.WindowsReference, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0));

        diagnostics.ActKexInit("client", kexInit);
        string[] ciphers = "chacha20-poly1305@openssh.com,aes256-ctr,aes192-ctr,aes128-ctr,aes256-cbc,rijndael-cbc@lysator.liu.se,aes192-cbc,aes128-cbc,arcfour128,arcfour,3des-cbc".Split(',');
        diagnostics.DiffList("cipher c2s", string.Join(',', ciphers), kexInit.CipherClientToServer);
        diagnostics.DiffList("cipher s2c", string.Join(',', ciphers), kexInit.CipherServerToClient);
        CollectionAssert.AreEqual(ciphers, kexInit.CipherClientToServer.ToArray());
        CollectionAssert.AreEqual(ciphers, kexInit.CipherServerToClient.ToArray());
        string[] macs = "hmac-sha2-256,hmac-sha2-256-etm@openssh.com,hmac-sha2-512,hmac-sha2-512-etm@openssh.com,hmac-sha1,hmac-sha1-etm@openssh.com,hmac-sha1-96,hmac-md5,hmac-md5-96".Split(',');
        diagnostics.DiffList("mac c2s", string.Join(',', macs), kexInit.MacClientToServer);
        diagnostics.DiffList("mac s2c", string.Join(',', macs), kexInit.MacServerToClient);
        CollectionAssert.AreEqual(macs, kexInit.MacClientToServer.ToArray());
        CollectionAssert.AreEqual(macs, kexInit.MacServerToClient.ToArray());
    }

    [TestMethod]
    [DataRow("Windows", false, "none")]
    [DataRow("Windows", true, "zlib,zlib@openssh.com,none")]
    [DataRow("OpenSSL", false, "none")]
    [DataRow("OpenSSL", true, "zlib,zlib@openssh.com,none")]
    public void ForClient_TodaysCatalogue_OffersTheCompressionListsMeasuredWithAndWithoutCompressedSsh(string platform, bool compressedSsh, string expected)
    {
        SshAlgorithmPreferences preset = platform == "Windows" ? SshAlgorithmPreferences.WindowsReference : SshAlgorithmPreferences.OpenSslReference;

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platform", platform);
        diagnostics.Arrange("--compressed-ssh", compressedSsh);

        SshKexInit kexInit = SshKexInit.ForClient(preset.WithCompression(compressedSsh), SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0));

        diagnostics.Act("compression c2s", string.Join(',', kexInit.CompressionClientToServer));
        diagnostics.Act("compression s2c", string.Join(',', kexInit.CompressionServerToClient));
        diagnostics.DiffList("compression c2s", expected, kexInit.CompressionClientToServer);
        diagnostics.DiffList("compression s2c", expected, kexInit.CompressionServerToClient);
        Assert.AreEqual(expected, string.Join(',', kexInit.CompressionClientToServer));
        Assert.AreEqual(expected, string.Join(',', kexInit.CompressionServerToClient));
    }

    [TestMethod]
    public void Parse_ReadsWhatToPayloadWrote()
    {
        SshKexInit sent = SshServerScript.OpenSshKexInit(kexInit => kexInit with
        {
            Cookie = [.. Enumerable.Range(1, 16).Select(value => (byte)value)],
            LanguagesClientToServer = ["en"],
            FirstKexPacketFollows = true,
            Reserved = 7,
        });

        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.ArrangeKexInit("sent", sent);
        diagnostics.Bytes("payload", sent.ToPayload());

        SshKexInit read = SshKexInit.Parse(sent.ToPayload());

        diagnostics.ActKexInit("read", read);
        diagnostics.Diff("cookie", sent.Cookie, read.Cookie);
        diagnostics.DiffList("kex", string.Join(',', sent.KeyExchange), read.KeyExchange);
        diagnostics.DiffList("languages c2s", "en", read.LanguagesClientToServer);
        diagnostics.Assert("first kex packet follows", true, read.FirstKexPacketFollows);
        diagnostics.Assert("reserved", 7u, read.Reserved);
        CollectionAssert.AreEqual(sent.Cookie, read.Cookie);
        CollectionAssert.AreEqual(sent.KeyExchange.ToArray(), read.KeyExchange.ToArray());
        CollectionAssert.AreEqual(sent.ServerHostKey.ToArray(), read.ServerHostKey.ToArray());
        CollectionAssert.AreEqual(sent.CipherServerToClient.ToArray(), read.CipherServerToClient.ToArray());
        CollectionAssert.AreEqual(sent.MacServerToClient.ToArray(), read.MacServerToClient.ToArray());
        CollectionAssert.AreEqual(sent.CompressionServerToClient.ToArray(), read.CompressionServerToClient.ToArray());
        CollectionAssert.AreEqual(new[] { "en" }, read.LanguagesClientToServer.ToArray());
        Assert.AreEqual(0, read.LanguagesServerToClient.Count);
        Assert.IsTrue(read.FirstKexPacketFollows);
        Assert.AreEqual(7u, read.Reserved);
    }

    [TestMethod]
    public void Parse_TruncatedPayload_ThrowsInvalidData()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] payload = SshServerScript.OpenSshKexInit().ToPayload();
        diagnostics.Bytes("truncated payload", payload[..^1]);
        diagnostics.Arrange("truncated length", $"{payload.Length - 1} of {payload.Length} bytes");

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => SshKexInit.Parse(payload[..^1]));

        diagnostics.Act("exception", failure.Message);
        diagnostics.Assert("exception type", nameof(InvalidDataException), failure.GetType().Name);
    }
}
