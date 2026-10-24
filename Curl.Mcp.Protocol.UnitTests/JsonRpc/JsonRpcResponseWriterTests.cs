using System.Text;
using System.Text.Json;

namespace Curl.Mcp.Protocol.JsonRpc;

[TestClass]
public sealed class JsonRpcResponseWriterTests
{
    private static JsonRpcRequestId ReadId(string line) =>
        ((JsonRpcRequest)new JsonRpcMessageParser().Parse(Encoding.UTF8.GetBytes(line))).Id;

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [TestMethod]
    public void WriteSuccess_NumericId_EchoesIdAndResult()
    {
        JsonRpcRequestId id = ReadId("""{"jsonrpc":"2.0","id":12345678901234567890123,"method":"m"}""");

        byte[] response = new JsonRpcResponseWriter().WriteSuccess(id, Json("""{"tools":[]}"""));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":12345678901234567890123,"result":{"tools":[]}}""", Encoding.UTF8.GetString(response));
    }

    [TestMethod]
    public void WriteSuccess_StringId_EchoesStringId()
    {
        JsonRpcRequestId id = ReadId("""{"jsonrpc":"2.0","id":"a-1","method":"m"}""");

        byte[] response = new JsonRpcResponseWriter().WriteSuccess(id, Json("{}"));

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"a-1","result":{}}""", Encoding.UTF8.GetString(response));
    }

    [TestMethod]
    [DataRow(JsonRpcErrorCodes.MethodNotFound, "Method not found")]
    [DataRow(JsonRpcErrorCodes.InvalidParams, "Invalid params")]
    [DataRow(JsonRpcErrorCodes.InternalError, "Internal error")]
    [DataRow(JsonRpcErrorCodes.ResourceNotFound, "Resource not found")]
    public void WriteError_WithId_WritesCodeAndMessage(int code, string message)
    {
        JsonRpcRequestId id = ReadId("""{"jsonrpc":"2.0","id":3,"method":"m"}""");

        byte[] response = new JsonRpcResponseWriter().WriteError(id, new JsonRpcError(code, message, null));

        Assert.AreEqual($$$"""{"jsonrpc":"2.0","id":3,"error":{"code":{{{code}}},"message":"{{{message}}}"}}""", Encoding.UTF8.GetString(response));
    }

    [TestMethod]
    public void WriteError_WithData_WritesData()
    {
        JsonRpcError error = new(JsonRpcErrorCodes.ResourceNotFound, "Resource not found", Json("""{"uri":"file:///dir/x"}"""));

        byte[] response = new JsonRpcResponseWriter().WriteError((JsonRpcRequestId?)null, error);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32002,"message":"Resource not found","data":{"uri":"file:///dir/x"}}}""", Encoding.UTF8.GetString(response));
    }

    [TestMethod]
    public void WriteError_ParseErrorMessage_WritesNullId()
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)new JsonRpcMessageParser().Parse("{"u8.ToArray());

        byte[] response = new JsonRpcResponseWriter().WriteError(invalid);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":null,"error":{"code":-32700,"message":"Parse error"}}""", Encoding.UTF8.GetString(response));
    }

    [TestMethod]
    public void WriteError_InvalidRequestWithId_EchoesId()
    {
        JsonRpcInvalidMessage invalid = (JsonRpcInvalidMessage)new JsonRpcMessageParser().Parse("""{"jsonrpc":"2.0","id":"q"}"""u8.ToArray());

        byte[] response = new JsonRpcResponseWriter().WriteError(invalid);

        Assert.AreEqual("""{"jsonrpc":"2.0","id":"q","error":{"code":-32600,"message":"Invalid Request"}}""", Encoding.UTF8.GetString(response));
    }
}
