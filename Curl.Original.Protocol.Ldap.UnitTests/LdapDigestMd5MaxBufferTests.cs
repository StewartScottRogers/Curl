using System.Buffers.Binary;
using System.Text;
using Curl.Original.Protocol.Ldap.Fakes;

namespace Curl.Original.Protocol.Ldap;

/// <summary>
/// Pins that a DIGEST-MD5 confidentiality layer never sends a sealed buffer longer than the
/// challenge's <c>maxbuf</c> (STD-122, BL-5440), splitting a longer message into several.
/// </summary>
[TestClass]
public sealed class LdapDigestMd5MaxBufferTests
{
    [TestMethod]
    [DataRow(",maxbuf=1024", 1630, 1024)]
    [DataRow(",maxbuf=1024", 5000, 1024)]
    [DataRow("", 70126, 65536)]
    [DataRow(",maxbuf=0", 70126, 65536)]
    [DataRow(",maxbuf=junk", 70126, 65536)]
    [DataRow(",maxbuf=64", 400, 64)]
    public async Task WriteAsync_LongMessage_SealsBuffersNoLongerThanMaxbuf(string maxbuf, int length, int limit)
    {
        byte[] challenge = Encoding.ASCII.GetBytes($"nonce=\"n\",qop=\"auth-conf\",cipher=\"3des\"{maxbuf}");
        LdapDigestMd5SecurityLayer digest = LdapDigestMd5Bind.SecurityLayerFor(challenge, "u", "p", "c")!;
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, digest);

        await layer.WriteAsync(new byte[length], CancellationToken.None);

        byte[] sent = connection.Sent;
        int offset = 0;
        int buffers = 0;
        while (offset < sent.Length)
        {
            int wrapped = BinaryPrimitives.ReadInt32BigEndian(sent.AsSpan(offset));
            Assert.IsLessThanOrEqualTo(limit, wrapped);
            offset += 4 + wrapped;
            buffers++;
        }

        Assert.AreEqual(sent.Length, offset);
        Assert.IsGreaterThan(0, buffers);
    }

    [TestMethod]
    public async Task WriteAsync_EmptyMessage_SendsOneBuffer()
    {
        var connection = new ScriptedConnection();
        var layer = new LdapSaslSecurityLayer(connection, LdapDigestMd5SecurityLayer.Create(new byte[16]));

        await layer.WriteAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None);

        Assert.IsGreaterThan(4, connection.Sent.Length);
    }
}
