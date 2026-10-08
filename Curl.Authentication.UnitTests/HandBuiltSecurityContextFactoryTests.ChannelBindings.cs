using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Authentication;

// <summary>
// Checks the channel bindings the hand-built Kerberos sends (BL-915): over HTTPS the
// authenticator checksum's Bnd is the MD5 of RFC 2744's structure holding
// tls-server-end-point: and the server certificate's hash, as curl 8.18.0 with MIT was measured
// sending (BL-832); without a certificate, plain HTTP, it is zeros; and a certificate whose
// signature names no hash fails with exit 91 before any ticket is looked for (BL-965).
// </summary>
public sealed partial class HandBuiltSecurityContextFactoryTests
{
    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public async Task ChannelBindings_Sha256RsaServerCertificate_SendsTheMd5OfItsTlsServerEndPointBindings(SecurityMechanism mechanism)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] certificate = TlsServerEndPointChannelBindingsTests.RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        diagnostics.Arrange("mechanism", mechanism);
        diagnostics.Bytes("server certificate", certificate);

        FakeGssAcceptor acceptor = await EstablishAsync(Request(mechanism) with { ServerCertificate = certificate });

        byte[] applicationData = [.. Encoding.ASCII.GetBytes("tls-server-end-point:"), .. SHA256.HashData(certificate)];
        byte[] structure = new byte[20 + applicationData.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(structure.AsSpan(16), (uint)applicationData.Length);
        applicationData.CopyTo(structure, 20);
        diagnostics.Bytes("bindings in the authenticator checksum", ChecksumBindings(acceptor));
        diagnostics.Act("bindings length", ChecksumBindings(acceptor).Length);
        diagnostics.Diff("bindings", MD5.HashData(structure), ChecksumBindings(acceptor));
        CollectionAssert.AreEqual(MD5.HashData(structure), ChecksumBindings(acceptor));
    }

    [TestMethod]
    public async Task ChannelBindings_NoServerCertificate_SendsZeros()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Arrange("server certificate", "none");

        FakeGssAcceptor acceptor = await EstablishAsync(Request(SecurityMechanism.Negotiate));

        diagnostics.Bytes("bindings in the authenticator checksum", ChecksumBindings(acceptor));
        diagnostics.Act("bindings length", ChecksumBindings(acceptor).Length);
        diagnostics.Diff("bindings", new byte[16], ChecksumBindings(acceptor));
        CollectionAssert.AreEqual(new byte[16], ChecksumBindings(acceptor));
    }

    [TestMethod]
    public async Task ChannelBindings_RsaPssServerCertificateWithoutTicket_FailsWithExit91BeforeLookingForOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] certificate = TlsServerEndPointChannelBindingsTests.RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        diagnostics.Arrange("mechanism", SecurityMechanism.Negotiate);
        diagnostics.Bytes("RSA-PSS server certificate", certificate);
        KerberosServiceTicketSource noCache = new(
            () => KerberosConfiguration.Empty,
            () => throw new AssertFailedException("The bindings fail before the cache is read."),
            _ => throw new AssertFailedException("No KDC client is needed."));
        using ISecurityContext context = Factory(noCache).Create(Request(SecurityMechanism.Negotiate) with { ServerCertificate = certificate });

        HttpAuthenticationFailedException failure = await Assert.ThrowsExactlyAsync<HttpAuthenticationFailedException>(
            async () => await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));

        diagnostics.Act("exit code", failure.ExitCode);
        diagnostics.Act("message", failure.Message);
        diagnostics.Assert("exit code", CurlExitCode.SslInvalidCertStatus, failure.ExitCode);
        diagnostics.Diff("message", "Could not find digest algorithm UNDEF (NID 0)", failure.Message);
        Assert.AreEqual(CurlExitCode.SslInvalidCertStatus, failure.ExitCode);
        Assert.AreEqual("Could not find digest algorithm UNDEF (NID 0)", failure.Message);
    }

    private static byte[] ChecksumBindings(FakeGssAcceptor acceptor) => acceptor.Authenticator!.Checksum!.Value.AsSpan(4, 16).ToArray();

    /// <summary>Runs the whole exchange for <paramref name="request" />, and gives the acceptor that read the initial token.</summary>
    private static async Task<FakeGssAcceptor> EstablishAsync(SecurityContextRequest request)
    {
        FakeGssAcceptor acceptor = new(KerberosEncryptionType.Aes256CtsHmacSha196);
        using ISecurityContext context = Factory(new FakeKdc()).Create(request);

        SecurityContextStep first = await context.NextTokenAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);
        bool negotiate = request.Mechanism == SecurityMechanism.Negotiate;
        acceptor.Accept(negotiate ? ReadNegTokenInit(first.Token).KerberosToken : first.Token);
        byte[] reply = negotiate
            ? new SpnegoNegotiationResponse(SpnegoNegotiationState.AcceptCompleted, SpnegoMechanism.KerberosV5, acceptor.Reply(), null).Encode()
            : acceptor.Reply();
        SecurityContextStep second = await context.NextTokenAsync(reply, CancellationToken.None);

        Assert.AreEqual(SecurityContextStatus.Completed, second.Status);
        return acceptor;
    }
}
