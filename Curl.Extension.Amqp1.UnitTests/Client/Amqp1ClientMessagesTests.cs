using Curl.Extension.Amqp1.Frames;
using Curl.Extension.Amqp1.Types;
using Curl.Protocol.Abstractions;
using static Curl.Extension.Amqp1.Client.Amqp1ServerBytes;

namespace Curl.Extension.Amqp1.Client;

[TestClass]
public sealed class Amqp1ClientMessagesTests
{
    [TestMethod]
    [DataRow("amqp:unauthorized-access", CurlExitCode.LoginDenied)]
    [DataRow("amqp:not-found", CurlExitCode.RemoteFileNotFound)]
    [DataRow("amqp:internal-error", CurlExitCode.WeirdServerReply)]
    public void PeerError_Condition_MapsToItsExitCode_Part2Section2_8_15(string condition, CurlExitCode exitCode)
    {
        var failure = Amqp1ClientMessages.PeerError(Amqp1CompositeType.Close, Error(new Amqp1Symbol(condition)));

        Assert.AreEqual(exitCode, failure.ExitCode);
        Assert.AreEqual($"AMQP 1.0 close error: {condition}", failure.Message);
    }

    [TestMethod]
    public void PeerError_WithDescription_NamesConditionAndDescription()
    {
        var failure = Amqp1ClientMessages.PeerError(Amqp1CompositeType.End, Error(new Amqp1Symbol("amqp:x"), "why"));

        Assert.AreEqual("AMQP 1.0 end error: amqp:x: why", failure.Message);
    }

    [TestMethod]
    public void PeerError_ConditionNotASymbol_IsMalformed()
    {
        Assert.ThrowsExactly<Amqp1MalformedEncodingException>(() => Amqp1ClientMessages.PeerError(Amqp1CompositeType.End, Error("amqp:x")));
    }
}
