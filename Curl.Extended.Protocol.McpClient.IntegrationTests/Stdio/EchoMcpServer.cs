using System.Diagnostics;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// A stand-in MCP server built as a C# file-based app in its own temporary directory, so the same
/// child process runs on Windows, Linux and macOS with no shell. It reads newline-delimited JSON-RPC
/// from standard input and answers every message with an id by a result holding its own process id.
/// Started with <see cref="ExitOnEndOfInputArgument"/> it exits when its standard input closes;
/// started without it, it keeps running after end of input until it is killed.
/// </summary>
internal sealed class EchoMcpServer : IDisposable
{
    /// <summary>The argument that makes the server exit when its standard input closes.</summary>
    public const string ExitOnEndOfInputArgument = "exit-on-end-of-input";

    private const string Source = """
        #:property TargetFramework=net10.0
        using System.Text;
        using System.Text.Json;

        var exitOnEndOfInput = args.Length > 0 && args[0] == "exit-on-end-of-input";
        using var reader = new StreamReader(Console.OpenStandardInput());
        using var output = Console.OpenStandardOutput();
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            using var message = JsonDocument.Parse(line);
            if (message.RootElement.TryGetProperty("id", out var id))
            {
                var response = "{\"jsonrpc\":\"2.0\",\"id\":" + id.GetRawText()
                    + ",\"result\":{\"processId\":" + Environment.ProcessId + "}}\n";
                await output.WriteAsync(Encoding.UTF8.GetBytes(response));
                await output.FlushAsync();
            }
        }

        if (!exitOnEndOfInput)
        {
            await Task.Delay(Timeout.Infinite);
        }
        """;

    private readonly string directory;

    private EchoMcpServer(string directory, string assemblyPath)
    {
        this.directory = directory;
        AssemblyPath = assemblyPath;
    }

    /// <summary>The <c>dotnet</c> host that builds and runs the server.</summary>
    public static string DotnetHost =>
        Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

    /// <summary>The built server's assembly, run as <c>dotnet &lt;AssemblyPath&gt;</c>.</summary>
    public string AssemblyPath { get; }

    /// <summary>Writes the server's source to a new temporary directory and builds it there.</summary>
    public static async Task<EchoMcpServer> BuildAsync()
    {
        var directory = Directory.CreateTempSubdirectory("curl-mcp-echo-").FullName;
        var sourcePath = Path.Combine(directory, "echo-mcp-server.cs");
        var outputDirectory = Path.Combine(directory, "out");
        await File.WriteAllTextAsync(sourcePath, Source);
        var startInfo = new ProcessStartInfo(DotnetHost)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = directory,
        };
        foreach (var argument in new[] { "build", sourcePath, "-o", outputDirectory })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var build = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet build did not start.");
        var standardOutput = build.StandardOutput.ReadToEndAsync();
        var standardError = build.StandardError.ReadToEndAsync();
        await build.WaitForExitAsync();
        if (build.ExitCode != 0)
        {
            throw new InvalidOperationException($"Building the echo MCP server failed:\n{await standardOutput}\n{await standardError}");
        }

        return new EchoMcpServer(directory, Path.Combine(outputDirectory, "echo-mcp-server.dll"));
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
