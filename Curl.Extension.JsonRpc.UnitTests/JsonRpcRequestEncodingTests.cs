using System.Text;

namespace Curl.Extension.JsonRpc;

[TestClass]
public sealed class JsonRpcRequestEncodingTests
{
    [TestMethod]
    public void Encode_OneCall_IsTheRequestObjectOfSection4()
    {
        Assert.AreEqual(
            "{\"jsonrpc\":\"2.0\",\"method\":\"subtract\",\"params\":[42,23],\"id\":1}",
            Text([new("subtract", "[42,23]", 1)]));
    }

    [TestMethod]
    public void Encode_ANotification_HasNoId_Section4_1()
    {
        Assert.AreEqual("{\"jsonrpc\":\"2.0\",\"method\":\"update\",\"params\":[1,2,3,4,5]}", Text([new("update", "[1,2,3,4,5]", null)]));
    }

    [TestMethod]
    public void Encode_ACallWithoutParams_OmitsTheMember_Section4()
    {
        Assert.AreEqual("{\"jsonrpc\":\"2.0\",\"method\":\"foobar\",\"id\":1}", Text([new("foobar", null, 1)]));
    }

    [TestMethod]
    public void Encode_SeveralCalls_IsABatchArray_Section6()
    {
        Assert.AreEqual(
            "[{\"jsonrpc\":\"2.0\",\"method\":\"sum\",\"params\":[1,2,4],\"id\":1},{\"jsonrpc\":\"2.0\",\"method\":\"notify_hello\",\"params\":[7]}]",
            Text([new("sum", "[1,2,4]", 1), new("notify_hello", "[7]", null)]));
    }

    private static string Text(JsonRpcCall[] calls) => Encoding.UTF8.GetString(JsonRpcRequestEncoding.Encode(calls));
}
