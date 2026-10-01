using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SshConnectionFailureTests
{
    [TestMethod]
    public void Is_EachFailureOfTheConnectionOrItsPackets_IsTrue()
    {
        Exception[] failures =
        [
            new EndOfStreamException(),
            new InvalidDataException(),
            new SshPacketAuthenticationException(Libssh2ErrorCode.InvalidMac),
            new SshPacketLengthException(Libssh2ErrorCode.OutOfBoundary, uint.MaxValue),
        ];

        foreach (Exception failure in failures)
        {
            Assert.IsTrue(SshConnectionFailure.Is(failure), failure.GetType().Name);
        }
    }

    [TestMethod]
    public void Is_TheLocalOutputsIOException_IsFalse()
    {
        Assert.IsFalse(SshConnectionFailure.Is(new IOException("disk full")));
    }
}
