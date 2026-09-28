using System.Security.Cryptography;
using static Curl.Tls.Rfc8448Messages;

namespace Curl.Tls;

/// <summary>
/// Replays the key schedule of RFC 8448 sections 3 and 4: every secret, key, IV, Finished
/// and binder the traces print, derived from their inputs and from transcript hashes of
/// the traces' own handshake messages.
/// </summary>
[TestClass]
public sealed class Rfc8448KeyScheduleTests
{
    // Section 3: the x25519 shared secret, the IKM of "extract secret handshake".
    private const string SimpleSharedSecret = "8bd4054fb55b9d63fdfbacf9f04b9f0d35e6d63f537563efd46272900f89492d";

    private const string SimpleEarlySecret = "33ad0a1c607ec03b09e6cd9893680ce210adf300aa1f2660e1b22e10f170f92a";
    private const string SimpleHandshakeSecret = "1dc826e93606aa6fdc0aadc12f741b01046aa6b99f691ed221a9f0ca043fbeac";
    private const string SimpleMasterSecret = "18df06843d13a08bf2a449844c5f8a478001bc4d4c627984d5a41da8d0402919";
    private const string SimpleClientHandshakeTrafficSecret = "b3eddb126e067f35a780b3abf45e2d8f3b1a950738f52e9600746a0e27a55a21";
    private const string SimpleServerHandshakeTrafficSecret = "b67b7d690cc16c4e75e54213cb2d37b4e9c912bcded9105d42befd59d391ad38";
    private const string SimpleClientApplicationTrafficSecret = "9e40646ce79a7f9dc05af8889bce6552875afa0b06df0087f792ebb7c17504a5";
    private const string SimpleServerApplicationTrafficSecret = "a11af9f05531f856ad47116b45a950328204b4f44bfb6b3a4b4f1f3fcb631643";
    private const string SimpleResumptionMasterSecret = "7df235f2031d2a051287d02b0241b0bfdaf86cc856231f2d5aba46c434ec196c";
    private const string SimpleResumptionPreSharedKey = "4ecd0eb6ec3b4d87f5d6028f922ca4c5851a277fd41311c9e62d2c9492e1c4f3";

    private const string ResumedEarlySecret = "9b2188e9b2fc6d64d71dc329900e20bb41915000f678aa839cbb797cb7d8332c";
    private const string ResumedClientEarlyTrafficSecret = "3fbbe6a60deb66c30a32795aba0eff7eaa10105586e7be5c09678d63b6caab62";

    private static readonly Tls13KeySchedule Schedule = Tls13KeySchedule.Sha256;

    // Section 3: {server} extract secret "early".
    [TestMethod]
    public void ComputeEarlySecretWithoutPreSharedKeyMatchesTheSimpleHandshake() =>
        AssertHex(SimpleEarlySecret, Schedule.ComputeEarlySecret(null));

    // Section 3: {server} derive secret for handshake "tls13 derived", then extract secret "handshake".
    [TestMethod]
    public void ComputeHandshakeSecretMatchesTheSimpleHandshake()
    {
        AssertHex("6f2615a108c702c5678f54fc9dbab69716c076189c48250cebeac3576c3611ba", Schedule.DeriveSecret(Hex(SimpleEarlySecret), "derived", Sha256Of()));
        AssertHex(SimpleHandshakeSecret, Schedule.ComputeHandshakeSecret(Hex(SimpleEarlySecret), Hex(SimpleSharedSecret)));
    }

    // Section 3: {server} derive secret for master "tls13 derived", then extract secret "master".
    [TestMethod]
    public void ComputeMasterSecretMatchesTheSimpleHandshake()
    {
        AssertHex("43de77e0c77713859a944db9db2590b53190a65b3ee2e4f12dd7a0bb7ce254b4", Schedule.DeriveSecret(Hex(SimpleHandshakeSecret), "derived", Sha256Of()));
        AssertHex(SimpleMasterSecret, Schedule.ComputeMasterSecret(Hex(SimpleHandshakeSecret)));
    }

    // Section 3: {server} derive secret "tls13 c hs traffic" and "tls13 s hs traffic".
    [TestMethod]
    public void HandshakeTrafficSecretsMatchTheSimpleHandshake()
    {
        byte[] serverHelloHash = Sha256Of(SimpleClientHello, SimpleServerHello);

        AssertHex("860c06edc07858ee8e78f0e7428c58edd6b43f2ca3e6e95f02ed063cf0e1cad8", serverHelloHash);
        AssertHex(SimpleClientHandshakeTrafficSecret, Schedule.DeriveClientHandshakeTrafficSecret(Hex(SimpleHandshakeSecret), serverHelloHash));
        AssertHex(SimpleServerHandshakeTrafficSecret, Schedule.DeriveServerHandshakeTrafficSecret(Hex(SimpleHandshakeSecret), serverHelloHash));
    }

    // Section 3: {server} derive write traffic keys for handshake data, and read traffic
    // keys for handshake data (the client's).
    [TestMethod]
    public void HandshakeTrafficKeysMatchTheSimpleHandshake()
    {
        Tls13TrafficKeys server = Schedule.DeriveTrafficKeys(Hex(SimpleServerHandshakeTrafficSecret), 16);
        Tls13TrafficKeys client = Schedule.DeriveTrafficKeys(Hex(SimpleClientHandshakeTrafficSecret), 16);

        AssertHex("3fce516009c21727d0f2e4e86ee403bc", server.Key);
        AssertHex("5d313eb2671276ee13000b30", server.Iv);
        AssertHex("dbfaa693d1762c5b666af5d950258d01", client.Key);
        AssertHex("5bd3c71b836e0b76bb73265f", client.Iv);
    }

    // Section 3: {server} calculate finished "tls13 finished", over the transcript through
    // CertificateVerify.
    [TestMethod]
    public void ServerFinishedMatchesTheSimpleHandshake()
    {
        byte[] certificateVerifyHash = Sha256Of(SimpleClientHello, SimpleServerHello, SimpleEncryptedExtensions, SimpleCertificate, SimpleCertificateVerify);

        AssertHex("008d3b66f816ea559f96b537e885c31fc068bf492c652f01f288a1d8cdc19fc8", Schedule.DeriveFinishedKey(Hex(SimpleServerHandshakeTrafficSecret)));
        AssertHex(SimpleServerFinished[8..], Schedule.ComputeFinishedVerifyData(Hex(SimpleServerHandshakeTrafficSecret), certificateVerifyHash));
    }

    // Section 3: {server} derive secret "tls13 c ap traffic", "tls13 s ap traffic" and
    // "tls13 exp master".
    [TestMethod]
    public void ApplicationTrafficSecretsMatchTheSimpleHandshake()
    {
        byte[] serverFinishedHash = Sha256Of(SimpleClientHello, SimpleServerHello, SimpleEncryptedExtensions, SimpleCertificate, SimpleCertificateVerify, SimpleServerFinished);

        AssertHex("9608102a0f1ccc6db6250b7b7e417b1a000eaada3daae4777a7686c9ff83df13", serverFinishedHash);
        AssertHex(SimpleClientApplicationTrafficSecret, Schedule.DeriveClientApplicationTrafficSecret(Hex(SimpleMasterSecret), serverFinishedHash));
        AssertHex(SimpleServerApplicationTrafficSecret, Schedule.DeriveServerApplicationTrafficSecret(Hex(SimpleMasterSecret), serverFinishedHash));
        AssertHex("fe22f881176eda18eb8f44529e6792c50c9a3f89452f68d8ae311b4309d3cf50", Schedule.DeriveExporterMasterSecret(Hex(SimpleMasterSecret), serverFinishedHash));
    }

    // Section 3: {server} derive write traffic keys for application data, and {client}
    // derive write traffic keys for application data.
    [TestMethod]
    public void ApplicationTrafficKeysMatchTheSimpleHandshake()
    {
        Tls13TrafficKeys server = Schedule.DeriveTrafficKeys(Hex(SimpleServerApplicationTrafficSecret), 16);
        Tls13TrafficKeys client = Schedule.DeriveTrafficKeys(Hex(SimpleClientApplicationTrafficSecret), 16);

        AssertHex("9f02283b6c9c07efc26bb9f2ac92e356", server.Key);
        AssertHex("cf782b88dd83549aadf1e984", server.Iv);
        AssertHex("17422dda596ed5d9acd890e3c63f5051", client.Key);
        AssertHex("5b78923dee08579033e523d9", client.Iv);
    }

    // Section 3: {client} calculate finished "tls13 finished", over the transcript through
    // the server's Finished.
    [TestMethod]
    public void ClientFinishedMatchesTheSimpleHandshake()
    {
        byte[] serverFinishedHash = Sha256Of(SimpleClientHello, SimpleServerHello, SimpleEncryptedExtensions, SimpleCertificate, SimpleCertificateVerify, SimpleServerFinished);

        AssertHex("b80ad01015fb2f0bd65ff7d4da5d6bf83f84821d1f87fdc7d3c75b5a7b42d9c4", Schedule.DeriveFinishedKey(Hex(SimpleClientHandshakeTrafficSecret)));
        AssertHex(SimpleClientFinished[8..], Schedule.ComputeFinishedVerifyData(Hex(SimpleClientHandshakeTrafficSecret), serverFinishedHash));
    }

    // Section 3: {client} derive secret "tls13 res master", and {server} generate
    // resumption secret "tls13 resumption" with the ticket's nonce 00 00.
    [TestMethod]
    public void ResumptionSecretsMatchTheSimpleHandshake()
    {
        byte[] clientFinishedHash = Sha256Of(SimpleClientHello, SimpleServerHello, SimpleEncryptedExtensions, SimpleCertificate, SimpleCertificateVerify, SimpleServerFinished, SimpleClientFinished);

        AssertHex("209145a96ee8e2a122ff810047cc952684658d6049e86429426db87c54ad143d", clientFinishedHash);
        AssertHex(SimpleResumptionMasterSecret, Schedule.DeriveResumptionMasterSecret(Hex(SimpleMasterSecret), clientFinishedHash));
        AssertHex(SimpleResumptionPreSharedKey, Schedule.DeriveResumptionPreSharedKey(Hex(SimpleResumptionMasterSecret), [0x00, 0x00]));
    }

    // Section 4: {client} extract secret "early" from the resumption PSK.
    [TestMethod]
    public void ComputeEarlySecretWithPreSharedKeyMatchesTheResumedHandshake() =>
        AssertHex(ResumedEarlySecret, Schedule.ComputeEarlySecret(Hex(SimpleResumptionPreSharedKey)));

    // Section 4: {client} calculate PSK binder, over the ClientHello without its binders
    // list (two-byte length, one-byte length, 32-byte binder).
    [TestMethod]
    public void ComputePskBinderMatchesTheResumedHandshake()
    {
        byte[] clientHello = Hex(ResumedClientHello);
        byte[] truncatedClientHelloHash = SHA256.HashData(clientHello.AsSpan(0, clientHello.Length - 35));
        byte[] binderKey = Schedule.DeriveResumptionBinderKey(Hex(ResumedEarlySecret));

        AssertHex("63224b2e4573f2d3454ca84b9d009a04f6be9e05711a8396473aefa01e924a14", truncatedClientHelloHash);
        AssertHex("69fe131a3bbad5d63c64eebcc30e395b9d8107726a13d074e389dbc8a4e47256", binderKey);
        AssertHex("5588673e72cb59c87d220caffe94f2dea9a3b1609f7d50e90a48227db9ed7eaa", Schedule.DeriveFinishedKey(binderKey));
        AssertHex("3add4fb2d8fdf822a0ca3cf7678ef5e88dae990141c5924d57bb6fa31b9e5f9d", Schedule.ComputePskBinder(binderKey, truncatedClientHelloHash));
    }

    // Section 4: {client} derive secret "tls13 c e traffic" and "tls13 e exp master", and
    // derive write traffic keys for early application data.
    [TestMethod]
    public void EarlyTrafficSecretsAndKeysMatchTheResumedHandshake()
    {
        byte[] clientHelloHash = Sha256Of(ResumedClientHello);

        AssertHex("08ad0fa05d7c7233b1775ba2ff9f4c5b8b59276b7f227f13a976245f5d960913", clientHelloHash);
        AssertHex(ResumedClientEarlyTrafficSecret, Schedule.DeriveClientEarlyTrafficSecret(Hex(ResumedEarlySecret), clientHelloHash));
        AssertHex("b2026866610937d7423e5be90862ccf24c0e6091186d34f812089ff5be2ef7df", Schedule.DeriveEarlyExporterMasterSecret(Hex(ResumedEarlySecret), clientHelloHash));

        Tls13TrafficKeys early = Schedule.DeriveTrafficKeys(Hex(ResumedClientEarlyTrafficSecret), 16);
        AssertHex("920205a5b7bf2115e6fc5c2942834f54", early.Key);
        AssertHex("6d475f0993c8e564610db2b9", early.Iv);
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static void AssertHex(string expected, byte[] actual) => Assert.AreEqual(expected, Convert.ToHexStringLower(actual));

    private static byte[] Sha256Of(params string[] messages)
    {
        using TranscriptHash transcript = Schedule.CreateTranscriptHash();
        foreach (string message in messages)
        {
            transcript.Append(Hex(message));
        }

        return transcript.GetCurrentHash();
    }
}
