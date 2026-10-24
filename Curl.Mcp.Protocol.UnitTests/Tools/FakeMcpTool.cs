using System.Text.Json;
using Curl.Mcp.Protocol.Model;

namespace Curl.Mcp.Protocol.Tools;

/// <summary>A tool that runs the function a test gives it and records the arguments and token of its last call.</summary>
internal sealed class FakeMcpTool(string name, Func<JsonElement, CancellationToken, McpToolCallResult> call, string description = "A fake tool.", string inputSchemaJson = """{"type":"object"}""", McpToolAnnotations? annotations = null) : IMcpTool
{
    public string Name => name;

    public string Description => description;

    public string InputSchemaJson => inputSchemaJson;

    public McpToolAnnotations? Annotations => annotations;

    public JsonElement? LastArguments { get; private set; }

    public CancellationToken LastCancellationToken { get; private set; }

    public ValueTask<McpToolCallResult> CallAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        LastArguments = arguments.Clone();
        LastCancellationToken = cancellationToken;
        return ValueTask.FromResult(call(arguments, cancellationToken));
    }

    public static FakeMcpTool Succeeding(string name, string text = "ok") => new(name, (_, _) => McpToolCallResult.Success(text));
}
