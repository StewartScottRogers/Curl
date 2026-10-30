namespace Curl.Extension.JsonRpc;

[TestClass]
public sealed class JsonRpcCallListTests
{
    [TestMethod]
    public void Parse_AMethodAlone_IsOneCallWithoutParamsAndId1_Section4()
    {
        CollectionAssert.AreEqual(new[] { new JsonRpcCall("get_data", null, 1) }, JsonRpcCallList.Parse("  get_data  ").ToArray());
    }

    [TestMethod]
    public void Parse_ParamsByPositionOrByName_AreKeptAsGiven_Section4_2()
    {
        CollectionAssert.AreEqual(new[] { new JsonRpcCall("subtract", "[42, 23]", 1) }, JsonRpcCallList.Parse("subtract [42, 23]").ToArray());
        CollectionAssert.AreEqual(new[] { new JsonRpcCall("subtract", "{\"minuend\":42}", 1) }, JsonRpcCallList.Parse("subtract{\"minuend\":42}").ToArray());
    }

    [TestMethod]
    public void Parse_CallsSeparatedBySemicolons_AreABatchWithIdsSkippingNotifications_Sections4_1And6()
    {
        JsonRpcCall[] expected =
        [
            new("sum", "[1,2,4]", 1),
            new("notify_hello", "[7]", null),
            new("echo", "[\"a;b\"]", 2),
            new("get_data", null, 3),
        ];

        CollectionAssert.AreEqual(expected, JsonRpcCallList.Parse("sum [1,2,4]; !notify_hello [7];echo [\"a;b\"]\t;\nget_data;").ToArray());
    }

    [TestMethod]
    [DataRow("", "--jsonrpc names no method.")]
    [DataRow(" \r\n", "--jsonrpc names no method.")]
    [DataRow("sum;;get_data", "--jsonrpc gives a call with no method name.")]
    [DataRow("! [1]", "--jsonrpc gives a call with no method name.")]
    [DataRow("[1]", "--jsonrpc gives a call with no method name.")]
    [DataRow("sum [1,", "--jsonrpc: the params of sum are not valid JSON.")]
    [DataRow("sum [1,}", "--jsonrpc: the params of sum are not valid JSON.")]
    [DataRow("sum 42", "--jsonrpc: the call to sum must be followed by its params as a JSON array or object (JSON-RPC 2.0 section 4.2), ';' or the end.")]
    [DataRow("sum [1] [2]", "--jsonrpc: the call to sum must be followed by its params as a JSON array or object (JSON-RPC 2.0 section 4.2), ';' or the end.")]
    public void Parse_AValueThatNamesNoValidCall_Fails(string value, string message)
    {
        Assert.AreEqual(message, Assert.ThrowsExactly<FormatException>(() => JsonRpcCallList.Parse(value)).Message);
    }
}
