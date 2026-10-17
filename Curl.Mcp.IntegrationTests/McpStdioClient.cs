using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Curl.Mcp;

/// <summary>
/// A hand-written MCP client over standard input and output (ADR-0548 keeps the SDK in Curl.Mcp):
/// starts the built Curl.Mcp as a child process with <c>CURL_MCP_ROOT</c> set, sends newline-delimited
/// JSON-RPC requests and reads each response by its id. Standard error is kept for failure messages.
/// </summary>
internal sealed class McpStdioClient : IAsyncDisposable
{
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);

    private readonly Process process;
    private readonly StringBuilder standardError = new();
    private int nextId;

    private McpStdioClient(Process process)
    {
        this.process = process;
    }

    /// <summary>What the server has written to standard error so far.</summary>
    internal string StandardError
    {
        get
        {
            lock (standardError)
            {
                return standardError.ToString();
            }
        }
    }

    /// <summary>
    /// Starts Curl.Mcp from the test's output folder through the <c>dotnet</c> host that runs the tests,
    /// with the test's runtime configuration and dependency file, which carry every assembly Curl.Mcp needs.
    /// </summary>
    /// <param name="root">The folder <c>CURL_MCP_ROOT</c> names.</param>
    /// <returns>The started client, not yet initialized.</returns>
    internal static McpStdioClient Start(string root)
    {
        string directory = AppContext.BaseDirectory;
        string testName = typeof(McpStdioClient).Assembly.GetName().Name!;
        ProcessStartInfo startInfo = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            WorkingDirectory = directory,
        };
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(Path.Combine(directory, $"{testName}.runtimeconfig.json"));
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(Path.Combine(directory, $"{testName}.deps.json"));
        startInfo.ArgumentList.Add(Path.Combine(directory, "Curl.Mcp.dll"));
        return Start(startInfo, root);
    }

    /// <summary>Starts a native-AOT Curl.Mcp binary directly, with no <c>dotnet</c> host.</summary>
    /// <param name="binary">The published native binary.</param>
    /// <param name="root">The folder <c>CURL_MCP_ROOT</c> names.</param>
    /// <returns>The started client, not yet initialized.</returns>
    internal static McpStdioClient StartNative(string binary, string root)
    {
        ProcessStartInfo startInfo = new(binary)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(binary)!,
        };
        return Start(startInfo, root);
    }

    private static McpStdioClient Start(ProcessStartInfo startInfo, string root)
    {
        startInfo.Environment[McpConfinementRoot.VariableName] = root;
        Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Curl.Mcp did not start.");
        McpStdioClient client = new(process);
        process.ErrorDataReceived += (_, line) => client.AppendStandardError(line.Data);
        process.BeginErrorReadLine();
        return client;
    }

    /// <summary>Sends <c>initialize</c> and then the <c>notifications/initialized</c> notification.</summary>
    /// <returns>The <c>initialize</c> result.</returns>
    internal async Task<JsonElement> InitializeAsync()
    {
        JsonElement result = await RequestAsync("initialize", new JsonObject
        {
            ["protocolVersion"] = "2025-06-18",
            ["capabilities"] = new JsonObject(),
            ["clientInfo"] = new JsonObject { ["name"] = "Curl.Mcp.IntegrationTests", ["version"] = "1.0" },
        });
        await WriteAsync(new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "notifications/initialized" });
        return result;
    }

    /// <summary>Calls one tool.</summary>
    /// <param name="name">The tool's name.</param>
    /// <param name="arguments">The tool's arguments.</param>
    /// <returns>The <c>tools/call</c> result.</returns>
    internal Task<JsonElement> CallToolAsync(string name, JsonObject arguments) =>
        RequestAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments });

    /// <summary>Sends one request and reads lines until the response with its id arrives.</summary>
    /// <param name="method">The JSON-RPC method.</param>
    /// <param name="parameters">The request's parameters, or <see langword="null" /> for none.</param>
    /// <returns>The response's <c>result</c>.</returns>
    internal async Task<JsonElement> RequestAsync(string method, JsonObject? parameters = null)
    {
        int id = ++nextId;
        JsonObject request = new() { ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method };
        if (parameters is not null)
        {
            request["params"] = parameters;
        }

        await WriteAsync(request);
        using CancellationTokenSource timeout = new(ResponseTimeout);
        while (true)
        {
            string line = await process.StandardOutput.ReadLineAsync(timeout.Token)
                ?? throw new InvalidOperationException($"Curl.Mcp closed standard output before answering {method}. Standard error:\n{StandardError}");
            using JsonDocument response = JsonDocument.Parse(line);
            if (!response.RootElement.TryGetProperty("id", out JsonElement responseId) || responseId.ValueKind != JsonValueKind.Number || responseId.GetInt32() != id)
            {
                continue;
            }

            if (response.RootElement.TryGetProperty("error", out JsonElement error))
            {
                throw new InvalidOperationException($"{method} failed: {error.GetRawText()}");
            }

            return response.RootElement.GetProperty("result").Clone();
        }
    }

    /// <summary>Closes standard input, so the server stops, and kills it if it has not exited within ten seconds.</summary>
    /// <returns>A task that completes when the process has exited.</returns>
    public async ValueTask DisposeAsync()
    {
        process.StandardInput.Close();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }

        process.Dispose();
    }

    private async Task WriteAsync(JsonObject message)
    {
        await process.StandardInput.WriteLineAsync(message.ToJsonString());
        await process.StandardInput.FlushAsync();
    }

    private void AppendStandardError(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (standardError)
        {
            standardError.AppendLine(line);
        }
    }
}
