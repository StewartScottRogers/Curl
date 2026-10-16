using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Curl.Mcp;

/// <summary>
/// Publishes Curl.Mcp as native AOT for the machine's own runtime, once for the class, and drives the
/// native binary over standard input and output (BL-2486, ADR-0599): <c>initialize</c>, <c>tools/list</c>
/// and a <c>transfer</c> call carrying one request header against a loopback listener. Before ADR-0599 the
/// native binary stopped before <c>initialize</c>, because the tools' parameter types were unresolved without
/// reflection. It runs the SDK and a child process and sends bytes over a socket, so it is an Integration
/// test (ADR-0421).
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlMcpNativeAotIntegrationTests
{
    private static string publishFolder = string.Empty;
    private static string binary = string.Empty;

    private string root = string.Empty;

    [ClassInitialize]
    public static async Task PublishNativeAsync(TestContext context)
    {
        publishFolder = Path.Combine(Path.GetTempPath(), $"curl-mcp-aot-{Guid.NewGuid():N}");
        await PublishAsync(publishFolder);
        binary = Path.Combine(publishFolder, OperatingSystem.IsWindows() ? "Curl.Mcp.exe" : "Curl.Mcp");
    }

    [ClassCleanup]
    public static void DeletePublishFolder()
    {
        if (Directory.Exists(publishFolder))
        {
            Directory.Delete(publishFolder, recursive: true);
        }
    }

    [TestInitialize]
    public void CreateRoot()
    {
        root = Path.Combine(Path.GetTempPath(), $"curl-mcp-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(root, recursive: true);

    [TestMethod]
    public void Publish_MakesANativeBinaryWithNoManagedCurlMcpAssembly()
    {
        Assert.IsTrue(File.Exists(binary), binary);
        Assert.IsFalse(File.Exists(Path.Combine(publishFolder, "Curl.Mcp.dll")));
    }

    [TestMethod]
    public async Task NativeBinary_AnswersInitializeAndListsTheFiveTools()
    {
        await using McpStdioClient client = McpStdioClient.StartNative(binary, root);

        JsonElement initialize = await client.InitializeAsync();
        JsonElement list = await client.RequestAsync("tools/list");

        Assert.AreEqual("Curl.Mcp", initialize.GetProperty("serverInfo").GetProperty("name").GetString(), client.StandardError);
        string[] names = [.. list.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()!).Order(StringComparer.Ordinal)];
        CollectionAssert.AreEqual(new[] { "build_url", "curl", "parse_url", "transfer", "version" }, names);
    }

    [TestMethod]
    public async Task NativeBinary_TransferWithOneHeader_SendsItAndReturnsTheResponse()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string> served = LoopbackHttpResponder.ServeOneResponseAsync(listener, "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 6\r\nConnection: close\r\n\r\nnative");
        await using McpStdioClient client = McpStdioClient.StartNative(binary, root);
        await client.InitializeAsync();

        JsonElement result = await client.CallToolAsync("transfer", new JsonObject
        {
            ["url"] = $"http://127.0.0.1:{port}/aot",
            ["headers"] = new JsonArray(new JsonObject { ["name"] = "X-Aot", ["value"] = "yes" }),
        });

        string request = await served;
        string text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.IsFalse(result.TryGetProperty("isError", out JsonElement isError) && isError.GetBoolean(), text);
        StringAssert.StartsWith(request, "GET /aot HTTP/1.1\r\n");
        StringAssert.Contains(request, "\r\nX-Aot: yes\r\n");
        using JsonDocument json = JsonDocument.Parse(text);
        Assert.AreEqual(200, json.RootElement.GetProperty("statusCode").GetInt64());
        Assert.AreEqual("native", json.RootElement.GetProperty("body").GetProperty("content").GetString());
    }

    private static async Task PublishAsync(string output)
    {
        ProcessStartInfo startInfo = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        string[] arguments = ["publish", Path.Combine(RepositoryRoot(), "Curl.Mcp", "Curl.Mcp.csproj"), "-c", "Release", "-r", RuntimeInformation.RuntimeIdentifier, "-p:PublishAot=true", "-o", output, "-nologo"];
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // The AOT linker finds MSVC through vswhere, which is not on PATH by default.
        if (OperatingSystem.IsWindows())
        {
            string installer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer");
            startInfo.Environment["PATH"] = $"{Environment.GetEnvironmentVariable("PATH")}{Path.PathSeparator}{installer}";
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet publish did not start.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(15));
        await process.WaitForExitAsync(timeout.Token);
        string log = await standardOutput + await standardError;
        Assert.AreEqual(0, process.ExitCode, log);
        Assert.IsFalse(log.Contains("warning IL", StringComparison.Ordinal), log);
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Curl.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException($"No Curl.slnx above {AppContext.BaseDirectory}.");
    }
}
