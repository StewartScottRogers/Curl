using System.Text;

namespace Curl.Extension.JsonRpc;

[TestClass]
public sealed class JsonRpcReplyPairingTests
{
    private static readonly JsonRpcCall[] Batch =
    [
        new("sum", "[1,2,4]", 1),
        new("notify_hello", "[7]", null),
        new("subtract", "[42,23]", 2),
        new("get_data", null, 3),
    ];

    [TestMethod]
    public void Pair_ABatchAnsweredOutOfOrder_WritesEachResponseInCallOrderThenTheUnpaired_Section6()
    {
        string body = "[{\"jsonrpc\":\"2.0\",\"result\":[\"hello\", 5],\"id\":3},"
            + "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"},\"id\":null},"
            + "{\"jsonrpc\":\"2.0\",\"result\":19,\"id\":2},"
            + "{\"jsonrpc\":\"2.0\",\"result\":7,\"id\":1}]";

        Assert.AreEqual(
            "{\"jsonrpc\":\"2.0\",\"result\":7,\"id\":1}\n"
            + "{\"jsonrpc\":\"2.0\",\"result\":19,\"id\":2}\n"
            + "{\"jsonrpc\":\"2.0\",\"result\":[\"hello\", 5],\"id\":3}\n"
            + "{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"},\"id\":null}\n",
            Paired(body, Batch));
    }

    [TestMethod]
    public void Pair_OneResponseObject_IsWrittenOnItsOwnLine_Section5()
    {
        Assert.AreEqual("{\"jsonrpc\": \"2.0\", \"result\": 19, \"id\": 1}\n", Paired("{\"jsonrpc\": \"2.0\", \"result\": 19, \"id\": 1}", [new("subtract", "[42,23]", 1)]));
    }

    [TestMethod]
    public void Pair_ResponsesWhoseIdMatchesNoCall_FollowInTheOrderReceived_Section5()
    {
        string body = "[1,{\"result\":0},{\"id\":\"1\"},{\"id\":1.5},{\"id\":9},{\"id\":2}]";

        Assert.AreEqual("{\"id\":2}\n1\n{\"result\":0}\n{\"id\":\"1\"}\n{\"id\":1.5}\n{\"id\":9}\n", Paired(body, Batch));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("<html>Bad Gateway</html>")]
    [DataRow("42")]
    public void Pair_ABodyThatIsNotAJsonObjectOrArray_IsGivenUnchanged_Section6(string body)
    {
        Assert.AreEqual(body, Paired(body, Batch));
    }

    private static string Paired(string body, JsonRpcCall[] calls) =>
        Encoding.UTF8.GetString(JsonRpcReplyPairing.Pair(Encoding.UTF8.GetBytes(body), calls));
}
