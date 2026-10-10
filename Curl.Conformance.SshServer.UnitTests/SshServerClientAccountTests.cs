using System.Text;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerClientAccountTests
{
    [TestMethod]
    public void CreatePrivateKeyFile_FixedKey_IsAnOpenSshKeyV1FileOfSeventyCharacterLinesHoldingThePublicKey()
    {
        string text = Encoding.ASCII.GetString(SshServerClientAccount.CreatePrivateKeyFile());

        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        byte[] body = Convert.FromBase64String(string.Concat(lines[1..^1]));
        SshWireReader reader = new(body);
        Assert.AreEqual("-----BEGIN OPENSSH PRIVATE KEY-----", lines[0]);
        Assert.AreEqual("-----END OPENSSH PRIVATE KEY-----", lines[^1]);
        Assert.IsTrue(lines[1..^1].All(line => line.Length <= 70));
        CollectionAssert.AreEqual("openssh-key-v1\0"u8.ToArray(), reader.ReadBytes(15).ToArray());
        Assert.AreEqual("none", reader.ReadName());
        Assert.AreEqual("none", reader.ReadName());
        Assert.IsTrue(reader.ReadString().IsEmpty);
        Assert.AreEqual(1u, reader.ReadUInt32());
        CollectionAssert.AreEqual(SshServerClientAccount.PublicKeyBlob, reader.ReadString().ToArray());
        Assert.AreEqual(0, reader.ReadString().Length % 8);
    }

    [TestMethod]
    public void CreatePublicKeyFile_FixedKey_IsOneAuthorizedKeysLineWithTheBlob()
    {
        string line = Encoding.ASCII.GetString(SshServerClientAccount.CreatePublicKeyFile());

        string[] fields = line.TrimEnd('\n').Split(' ');
        Assert.EndsWith("\n", line);
        Assert.AreEqual("ssh-ed25519", fields[0]);
        CollectionAssert.AreEqual(SshServerClientAccount.PublicKeyBlob, Convert.FromBase64String(fields[1]));
        Assert.AreEqual("curltest@conformance", fields[2]);
    }

    [TestMethod]
    public void Verifies_SignatureFromSign_IsTrue()
    {
        byte[] signature = SshServerClientAccount.Sign("data"u8);

        Assert.IsTrue(SshServerClientAccount.Verifies(SshServerClientAccount.KeyAlgorithm, "data"u8, signature));
    }
}
