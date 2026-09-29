using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Curl.Kerberos;
using Curl.Protocol.Abstractions;

namespace Curl.Authentication;

// <summary>
// Checks the channel bindings the hand-built Kerberos sends (BL-915): over HTTPS the
// authenticator checksum's Bnd is the MD5 of RFC 2744's structure holding
// tls-server-end-point: and the server certificate's hash, as curl 8.18.0 with MIT was measured
// sending (BL-832); without a certificate, plain HTTP, it is zeros.
// </summary>
public sealed partial class HandBuiltSecurityContextFactoryTests
{
    [TestMethod]
    [DataRow(SecurityMechanism.Negotiate)]
    [DataRow(SecurityMechanism.Kerberos)]
    public async Task ChannelBindings_Sha256RsaServerCertificate_SendsTheMd5OfItsTlsServerEndPointBindings(SecurityMechanism mechanism)
    {
        byte[] certificate = TlsServerEndPointChannelBindingsTests.RsaCertificate(HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        FakeGssAcceptor acceptor = await EstablishAsync(Request(mechanism) with { ServerCertificate = certificate });

        byte[] applicationData = [.. Encoding.ASCII.GetBytes("tls-server-end-point:"), .. SHA256.HashData(certificate)];
        byte[] structure = new byte[20 + applicationData.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(structure.AsSpan(16), (uint)applicationData.Length);
        applicationData.CopyTo(structure, 20);
        CollectionAssert.AreEqual(MD5.HashData(structure), ChecksumBindings(acceptor));
    }

    [TestMethod]
    public async Task ChannelBindings_NoServerCertificate_SendsZeros()
    {
        FakeGssAcceptor acceptor = await EstablishAsync(Request(SecurityMechanism.Negotiate));

        CollectionAssert.AreEqual(new byte[16], ChecksumBindings(acceptor));
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
