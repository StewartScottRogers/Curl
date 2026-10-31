using System.Text;

namespace Curl.Extension.ConnectRpc;

[TestClass]
public sealed class ConnectRequestMessagesTests
{
    [TestMethod]
    public void Split_JsonValuesOneAfterAnother_GivesEachValue()
    {
        IReadOnlyList<ReadOnlyMemory<byte>> messages = ConnectRequestMessages.Split(Encoding.UTF8.GetBytes("{\"a\":[1,2]}\n {\"b\":\"}\"}{}"));

        CollectionAssert.AreEqual(
            new[] { "{\"a\":[1,2]}", "{\"b\":\"}\"}", "{}" },
            messages.Select(message => Encoding.UTF8.GetString(message.Span)).ToArray());
    }

    [TestMethod]
    public void Split_OnlyWhiteSpace_GivesNoMessages()
    {
        Assert.IsEmpty(ConnectRequestMessages.Split(Encoding.UTF8.GetBytes(" \n")));
    }

    [TestMethod]
    public void Split_ABodyThatIsNotJson_IsRefused()
    {
        FormatException refused = Assert.ThrowsExactly<FormatException>(() => ConnectRequestMessages.Split(Encoding.UTF8.GetBytes("{\"a\":")));

        StringAssert.StartsWith(refused.Message, "--connect-rpc:");
    }
}
