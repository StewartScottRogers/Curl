using System.Text;
using Curl.Protocol.Ssh;
using Curl.Protocol.Ssh.Authentication;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerUserAuthenticationTests
{
    private static readonly byte[] SessionIdentifier = [1, 2, 3, 4];

    [TestMethod]
    public void Answer_NoneMethod_FailsNamingPublicKeyAndPassword()
    {
        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(Request(SshServerClientAccount.User, "none", _ => { }), SessionIdentifier);

        SshWireReader reader = new(reply);
        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reader.ReadByte());
        Assert.AreEqual("publickey,password", reader.ReadName());
        Assert.IsFalse(reader.ReadBoolean());
        Assert.IsNull(user);
    }

    [TestMethod]
    public void Answer_AccountsPassword_SucceedsAsTheAccount()
    {
        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(PasswordRequest(SshServerClientAccount.User, SshServerClientAccount.Password), SessionIdentifier);

        CollectionAssert.AreEqual(new[] { SshAuthenticationMessageNumber.Success }, reply);
        Assert.AreEqual(SshServerClientAccount.User, user);
    }

    [TestMethod]
    [DataRow(SshServerClientAccount.User, "wrong")]
    [DataRow("someone-else", SshServerClientAccount.Password)]
    public void Answer_WrongPasswordOrUser_Fails(string userName, string password)
    {
        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(PasswordRequest(userName, password), SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
        Assert.IsNull(user);
    }

    [TestMethod]
    public void Answer_Ed25519KeyWithoutSignature_AnswersPublicKeyOkWithTheKey()
    {
        byte[] request = PublicKeyRequest(SshServerClientAccount.KeyAlgorithm, SshServerClientAccount.PublicKeyBlob, sign: null);

        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        SshWireReader reader = new(reply);
        Assert.AreEqual(SshAuthenticationMessageNumber.PublicKeyOk, reader.ReadByte());
        Assert.AreEqual(SshServerClientAccount.KeyAlgorithm, reader.ReadName());
        CollectionAssert.AreEqual(SshServerClientAccount.PublicKeyBlob, reader.ReadString().ToArray());
        Assert.IsNull(user);
    }

    [TestMethod]
    public void Answer_Ed25519KeySigned_SucceedsAsTheAccount()
    {
        byte[] request = PublicKeyRequest(SshServerClientAccount.KeyAlgorithm, SshServerClientAccount.PublicKeyBlob, data => SshServerClientAccount.Sign(data));

        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        CollectionAssert.AreEqual(new[] { SshAuthenticationMessageNumber.Success }, reply);
        Assert.AreEqual(SshServerClientAccount.User, user);
    }

    [TestMethod]
    [DataRow("ssh-rsa")]
    [DataRow("rsa-sha2-256")]
    [DataRow("rsa-sha2-512")]
    public void Answer_RsaKeySigned_SucceedsAsTheAccount(string algorithm)
    {
        byte[] request = PublicKeyRequest(algorithm, SshServerRsaClientKey.PublicKeyBlob, data => SshServerRsaClientKey.Sign(algorithm, data));

        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        CollectionAssert.AreEqual(new[] { SshAuthenticationMessageNumber.Success }, reply);
        Assert.AreEqual(SshServerClientAccount.User, user);
    }

    [TestMethod]
    public void Answer_RsaKeyWithoutSignature_AnswersPublicKeyOk()
    {
        byte[] request = PublicKeyRequest("rsa-sha2-512", SshServerRsaClientKey.PublicKeyBlob, sign: null);

        (byte[] reply, _) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.PublicKeyOk, reply[0]);
    }

    [TestMethod]
    public void Answer_SignatureOverOtherData_Fails()
    {
        byte[] request = PublicKeyRequest(SshServerClientAccount.KeyAlgorithm, SshServerClientAccount.PublicKeyBlob, _ => SshServerClientAccount.Sign("other"u8));

        (byte[] reply, string? user) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
        Assert.IsNull(user);
    }

    [TestMethod]
    public void Answer_RsaSignatureUnderAnotherAlgorithmsName_Fails()
    {
        byte[] request = PublicKeyRequest("rsa-sha2-256", SshServerRsaClientKey.PublicKeyBlob, data => SshServerRsaClientKey.Sign("rsa-sha2-512", data));

        (byte[] reply, _) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
    }

    [TestMethod]
    public void Answer_Ed25519SignatureOfTheWrongLength_Fails()
    {
        byte[] request = PublicKeyRequest(SshServerClientAccount.KeyAlgorithm, SshServerClientAccount.PublicKeyBlob, _ => SignatureBlob(SshServerClientAccount.KeyAlgorithm, new byte[10]));

        (byte[] reply, _) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
    }

    [TestMethod]
    public void Answer_RsaSignatureThatDoesNotVerify_Fails()
    {
        byte[] request = PublicKeyRequest("rsa-sha2-256", SshServerRsaClientKey.PublicKeyBlob, _ => SignatureBlob("rsa-sha2-256", new byte[256]));

        (byte[] reply, _) = SshServerUserAuthentication.Answer(request, SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
    }

    [TestMethod]
    [DataRow("ssh-ed25519", true)]
    [DataRow("rsa-sha2-256", false)]
    [DataRow("ecdsa-sha2-nistp256", false)]
    public void Answer_KeyTheAccountDoesNotHold_Fails(string algorithm, bool rsaBlob)
    {
        byte[] blob = rsaBlob ? SshServerRsaClientKey.PublicKeyBlob : SshServerClientAccount.PublicKeyBlob;

        (byte[] reply, _) = SshServerUserAuthentication.Answer(PublicKeyRequest(algorithm, blob, sign: null), SessionIdentifier);

        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, reply[0]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_NoneThenPassword_FailsFirstThenReturnsTheUser()
    {
        (SshServerTransport transport, SshPacketWriter client, SshPacketReader clientReader) = CreateSession();

        await client.WriteAsync(Request(SshServerClientAccount.User, "none", _ => { }), CancellationToken.None);
        await client.WriteAsync(PasswordRequest(SshServerClientAccount.User, SshServerClientAccount.Password), CancellationToken.None);
        string user = await SshServerUserAuthentication.AuthenticateAsync(transport, CancellationToken.None);

        Assert.AreEqual(SshServerClientAccount.User, user);
        Assert.AreEqual(SshAuthenticationMessageNumber.Failure, (await clientReader.ReadAsync(CancellationToken.None))[0]);
        Assert.AreEqual(SshAuthenticationMessageNumber.Success, (await clientReader.ReadAsync(CancellationToken.None))[0]);
    }

    [TestMethod]
    public async Task AuthenticateAsync_ClientSendsAnotherMessage_ThrowsInvalidDataException()
    {
        (SshServerTransport transport, SshPacketWriter client, _) = CreateSession();

        await client.WriteAsync(new byte[] { SshMessageNumber.Ignore, 0, 0, 0, 0 }, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await SshServerUserAuthentication.AuthenticateAsync(transport, CancellationToken.None));
    }

    private static (SshServerTransport Transport, SshPacketWriter Client, SshPacketReader ClientReader) CreateSession()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        return (new SshServerTransport(server, new SystemSshRandomSource()), new SshPacketWriter(client, new SystemSshRandomSource()), new SshPacketReader(new SshConnectionReader(client)));
    }

    private static byte[] PasswordRequest(string user, string password) => Request(user, "password", writer =>
    {
        writer.WriteBoolean(false);
        writer.WriteString(Encoding.UTF8.GetBytes(password));
    });

    // RFC 4252 section 7: the signature covers the session identifier and the request up to it.
    private static byte[] PublicKeyRequest(string algorithm, byte[] blob, Func<byte[], byte[]>? sign)
    {
        byte[] unsigned = Request(SshServerClientAccount.User, "publickey", writer =>
        {
            writer.WriteBoolean(sign is not null);
            writer.WriteString(Encoding.ASCII.GetBytes(algorithm));
            writer.WriteString(blob);
        });
        if (sign is null)
        {
            return unsigned;
        }

        SshWireWriter signedData = new();
        signedData.WriteString(SessionIdentifier);
        signedData.WriteBytes(unsigned);
        SshWireWriter request = new();
        request.WriteBytes(unsigned);
        request.WriteString(sign(signedData.ToArray()));
        return request.ToArray();
    }

    private static byte[] Request(string user, string method, Action<SshWireWriter> writeRest)
    {
        SshWireWriter request = new();
        request.WriteByte(SshAuthenticationMessageNumber.Request);
        request.WriteString(Encoding.UTF8.GetBytes(user));
        request.WriteString("ssh-connection"u8);
        request.WriteString(Encoding.ASCII.GetBytes(method));
        writeRest(request);
        return request.ToArray();
    }

    private static byte[] SignatureBlob(string algorithm, byte[] signature)
    {
        SshWireWriter writer = new();
        writer.WriteString(Encoding.ASCII.GetBytes(algorithm));
        writer.WriteString(signature);
        return writer.ToArray();
    }
}
