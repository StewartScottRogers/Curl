using Curl.Protocol.Ssh.PacketProtection;
using Curl.Protocol.Ssh.Transport;
using Curl.Testing;

namespace Curl.Protocol.Ssh;

[TestClass]
public sealed class SshConnectionFailureTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("failures", string.Join(", ", failures.Select(failure => failure.GetType().Name)));

        foreach (Exception failure in failures)
        {
            bool isConnectionFailure = SshConnectionFailure.Is(failure);
            Diagnostics.Act(failure.GetType().Name, isConnectionFailure);
            Diagnostics.Assert($"Is({failure.GetType().Name})", true, isConnectionFailure);
            Assert.IsTrue(isConnectionFailure, failure.GetType().Name);
        }
    }

    [TestMethod]
    public void Is_TheLocalOutputsIOException_IsFalse()
    {
        IOException failure = new("disk full");
        Diagnostics.Arrange("failure", "IOException: disk full");

        bool isConnectionFailure = SshConnectionFailure.Is(failure);

        Diagnostics.Act("Is", isConnectionFailure);
        Diagnostics.Assert("Is(IOException)", false, isConnectionFailure);
        Assert.IsFalse(isConnectionFailure);
    }
}
