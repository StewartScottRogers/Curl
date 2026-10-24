using System.Text;
using System.Text.Json;

namespace Curl.Mcp.Protocol.JsonRpc;

[TestClass]
public sealed class JsonRpcMessageParserTests
{
    private static JsonRpcIncoming Parse(string line) => new JsonRpcMessageParser().Parse(Encoding.UTF8.GetBytes(line));

    private static JsonRpcInvalidMessage AssertInvalid(JsonRpcIncoming incoming, int code)
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)incoming;
        Assert.AreEqual(code, invalid.Error.Code);
        return invalid;
    }

    [TestMethod]
    public void Parse_RequestWithNumericId_ReadsIdMethodAndParams()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":7,"method":"tools/list","params":{"cursor":"a"}}""");

        Assert.AreEqual("7", request.Id.Value.GetRawText());
        Assert.IsFalse(request.Id.IsString);
        Assert.AreEqual("tools/list", request.Method);
        Assert.AreEqual("a", request.Params!.Value.GetProperty("cursor").GetString());
    }

    [TestMethod]
    public void Parse_RequestWithStringId_ReadsStringId()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":"abc","method":"ping"}""");

        Assert.IsTrue(request.Id.IsString);
        Assert.AreEqual("abc", request.Id.Value.GetString());
        Assert.IsNull(request.Params);
    }

    [TestMethod]
    public void Parse_HugeNumericId_KeepsItsDigits()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":123456789012345678901234567890,"method":"ping"}""");

        Assert.AreEqual("123456789012345678901234567890", request.Id.Value.GetRawText());
    }

    [TestMethod]
    public void Parse_NoId_IsNotification()
    {
        JsonRpcNotification notification = (JsonRpcNotification)Parse("""{"jsonrpc":"2.0","method":"notifications/initialized","params":[1,2]}""");

        Assert.AreEqual("notifications/initialized", notification.Method);
        Assert.AreEqual(JsonValueKind.Array, notification.Params!.Value.ValueKind);
    }

    [TestMethod]
    public void Parse_NullParams_IsTreatedAsAbsent()
    {
        JsonRpcNotification notification = (JsonRpcNotification)Parse("""{"jsonrpc":"2.0","method":"m","params":null}""");

        Assert.IsNull(notification.Params);
    }

    [TestMethod]
    public void Parse_UnknownProperties_AreIgnored()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":1,"method":"m","extra":{"x":[true]}}""");

        Assert.AreEqual("m", request.Method);
    }

    [TestMethod]
    public void Parse_DuplicateKey_TakesLastValue()
    {
        JsonRpcRequest request = (JsonRpcRequest)Parse("""{"jsonrpc":"2.0","id":1,"method":"first","method":"second"}""");

        Assert.AreEqual("second", request.Method);
    }

    [TestMethod]
    [DataRow("{")]
    [DataRow("")]
    [DataRow("not json")]
    [DataRow("""{"jsonrpc":"2.0","id":1,"method":"m"} trailing""")]
    public void Parse_MalformedJson_IsParseErrorWithNoId(string line)
    {
        JsonRpcInvalidMessage invalid = AssertInvalid(Parse(line), JsonRpcErrorCodes.ParseError);

        Assert.AreEqual("Parse error", invalid.Error.Message);
        Assert.IsNull(invalid.Id);
    }

    [TestMethod]
    public void Parse_InvalidUtf8_IsParseError()
    {
        byte[] line = [.. Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":1,"method":"""), (byte)'"', 0xC3, 0x28, (byte)'"', (byte)'}'];

        AssertInvalid(new JsonRpcMessageParser().Parse(line), JsonRpcErrorCodes.ParseError);
    }

    [TestMethod]
    public void Parse_NestingDeeperThan64_IsParseError()
    {
        string line = """{"jsonrpc":"2.0","id":1,"method":"m","params":""" + new string('[', 100) + new string(']', 100) + "}";

        AssertInvalid(Parse(line), JsonRpcErrorCodes.ParseError);
    }

    [TestMethod]
    [DataRow("[]")]
    [DataRow("""[{"jsonrpc":"2.0","id":1,"method":"m"}]""")]
    [DataRow("42")]
    [DataRow("\"text\"")]
    [DataRow("null")]
    public void Parse_NonObject_IsInvalidRequestWithNoId(string line)
    {
        JsonRpcInvalidMessage invalid = AssertInvalid(Parse(line), JsonRpcErrorCodes.InvalidRequest);

        Assert.AreEqual("Invalid Request", invalid.Error.Message);
        Assert.IsNull(invalid.Id);
    }

    [TestMethod]
    [DataRow("""{"jsonrpc":"2.0","id":null,"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":true,"method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":{},"method":"m"}""")]
    public void Parse_IdNeitherStringNorNumber_IsInvalidRequestWithNoId(string line)
    {
        Assert.IsNull(AssertInvalid(Parse(line), JsonRpcErrorCodes.InvalidRequest).Id);
    }

    [TestMethod]
    [DataRow("""{"id":"x","method":"m"}""")]
    [DataRow("""{"jsonrpc":"1.0","id":"x","method":"m"}""")]
    [DataRow("""{"jsonrpc":2.0,"id":"x","method":"m"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":"x"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":"x","method":5}""")]
    [DataRow("""{"jsonrpc":"2.0","id":"x","method":"m","params":"p"}""")]
    [DataRow("""{"jsonrpc":"2.0","id":"x","method":"m","params":3}""")]
    public void Parse_InvalidRequestWithStringId_EchoesTheId(string line)
    {
        JsonRpcInvalidMessage invalid = AssertInvalid(Parse(line), JsonRpcErrorCodes.InvalidRequest);

        Assert.AreEqual("x", invalid.Id!.Value.GetString());
    }

    [TestMethod]
    public void Parse_MissingMethodWithNumericId_EchoesTheId()
    {
        JsonRpcInvalidMessage invalid = AssertInvalid(Parse("""{"jsonrpc":"2.0","id":9}"""), JsonRpcErrorCodes.InvalidRequest);

        Assert.AreEqual("9", invalid.Id!.Value.GetRawText());
    }

    [TestMethod]
    public void Parse_MissingMethodWithNoId_IsInvalidRequestWithNoId()
    {
        Assert.IsNull(AssertInvalid(Parse("""{"jsonrpc":"2.0"}"""), JsonRpcErrorCodes.InvalidRequest).Id);
    }

    [TestMethod]
    public void Parse_EveryPrefixOfAValidLine_NeverThrows()
    {
        byte[] line = Encoding.UTF8.GetBytes("""{"jsonrpc":"2.0","id":"é1","method":"m","params":{"a":[1,{"b":null}]}}""");
        JsonRpcMessageParser parser = new();

        for (int length = 0; length < line.Length; length++)
        {
            Assert.IsInstanceOfType<JsonRpcInvalidMessage>(parser.Parse(line.AsMemory(0, length)));
        }
    }
}
