using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ModelContextProtocol.Protocol;

namespace Curl.Mcp;

/// <summary>
/// Holds <see cref="CurlMcpJsonContext.ToolOptions" /> able to describe every tool parameter and result
/// type from its resolver chain alone, with no reflection, as native AOT needs (BL-2486, ADR-0599).
/// Under the JIT the serializer falls back to reflection, which hid that the chain once lacked these
/// contracts.
/// </summary>
[TestClass]
public sealed class CurlMcpJsonContextTests
{
    [TestMethod]
    [DataRow(typeof(McpTransferHeader[]))]
    [DataRow(typeof(McpTransferBody))]
    [DataRow(typeof(McpTransferToolResult))]
    [DataRow(typeof(McpCurlToolResult))]
    [DataRow(typeof(McpVersionToolResult))]
    [DataRow(typeof(McpParseUrlToolResult))]
    [DataRow(typeof(McpBuildUrlToolResult))]
    [DataRow(typeof(string[]))]
    public void ToolOptions_ResolverChainDescribesTheContractWithoutReflection(Type type)
    {
        JsonSerializerOptions options = CurlMcpJsonContext.ToolOptions;

        JsonTypeInfo? typeInfo = options.TypeInfoResolver!.GetTypeInfo(type, options);

        Assert.IsNotNull(typeInfo, type.Name);
    }

    [TestMethod]
    public void ToolOptions_ResolverChainStillDescribesTheSdkProtocolTypes()
    {
        JsonSerializerOptions options = CurlMcpJsonContext.ToolOptions;

        Assert.IsNotNull(options.TypeInfoResolver!.GetTypeInfo(typeof(CallToolResult), options));
    }

    [TestMethod]
    public void ToolOptions_IsBuiltOnceAndThenShared()
    {
        JsonSerializerOptions first = CurlMcpJsonContext.ToolOptions;

        JsonSerializerOptions second = CurlMcpJsonContext.ToolOptions;

        Assert.AreSame(first, second);
    }
}
