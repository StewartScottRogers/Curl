using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Curl.Mcp;

/// <summary>
/// Replays the recorded MCP conversations in <c>Golden/</c> against the built Curl.Mcp over standard
/// input and output. Each golden file is one conversation: the request lines sent and the response
/// line received for each (BL-3374). The files were recorded from the SDK-based server, so a
/// hand-written server can be proved to answer the same. It starts a child process and sends bytes
/// over a socket, so it is an Integration test (ADR-0421).
/// <para>
/// Request lines may hold <c>{root}</c> (the <c>CURL_MCP_ROOT</c> folder), <c>{outside}</c> (a folder
/// beside it, outside the root) and <c>{port}</c> (a loopback port, listening or closed as the
/// conversation says); the golden files keep these placeholders in the requests. Recorded responses
/// are normalised by named placeholders: <c>&lt;ROOT&gt;</c>, <c>&lt;OUTSIDE&gt;</c> and
/// <c>127.0.0.1:&lt;PORT&gt;</c> replace those values inside strings, and every property named
/// <c>elapsed*</c>, <c>speed*</c> or ending in <c>Seconds</c>, <c>Milliseconds</c> or <c>Time</c> in a response becomes
/// <c>&lt;TIMING&gt;</c>; the platform-dependent <c>features</c>, <c>tlsBackend</c>, <c>versionLine</c> and the per-run <c>primaryPort</c> become <c>&lt;PLATFORM&gt;</c>; CRLF becomes LF, every backslash in response text becomes a slash, and the text after <c>after N ms:</c> in a connect failure becomes <c>&lt;OS-ERROR&gt;</c> (with its byte counts <c>&lt;LENGTH&gt;</c>); a tool result's text that is a JSON object is stored as that object, normalised the same way. Responses are compared as JSON values: object property order is ignored and
/// array order is kept. Setting <c>CURL_MCP_RECORD_GOLDEN=1</c> rewrites the golden files from the
/// running server instead of comparing.
/// </para>
/// </summary>
[TestClass]
[TestCategory("Integration")]
[DoNotParallelize]
public sealed class CurlMcpGoldenConversationIntegrationTests
{
    private const string HelloResponse = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: 5\r\nConnection: close\r\n\r\nhello";
    private static readonly TimeSpan MaybeTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);
    private static readonly string GoldenFolder = Path.Combine(Path.GetDirectoryName(SourcePath())!, "Golden");

    private static readonly string[] Handshake =
    [
        Rpc(1, "initialize", Init("2025-06-18")),
        Notified(),
    ];

    // Name, listener kind (none, serve, closed) and the request lines; a leading '?' marks a line whose
    // answer is optional, a leading '!' a line with no answer.
    private static readonly (string Name, string Listener, string[] Lines)[] Conversations =
    [
        ("initialize-2025-11-25", "none", [Rpc(1, "initialize", Init("2025-11-25")), Notified()]),
        ("initialize-2025-06-18", "none", [Rpc(1, "initialize", Init("2025-06-18")), Notified()]),
        ("initialize-2024-11-05", "none", [Rpc(1, "initialize", Init("2024-11-05")), Notified()]),
        ("initialize-unknown-version", "none", [Rpc(1, "initialize", Init("1999-01-01")), Notified()]),
        ("ping", "none", [.. Handshake, Rpc(2, "ping", "{}")]),
        ("tools-list", "none", [.. Handshake, Rpc(2, "tools/list", "{}")]),
        ("tools-call-version", "none", [.. Handshake, Call(2, "version", "{}")]),
        ("tools-call-curl-success", "serve", [.. Handshake, Call(2, "curl", """{"args":["-sS","http://127.0.0.1:{port}/"]}""")]),
        ("tools-call-curl-nonzero-exit", "closed", [.. Handshake, Call(2, "curl", """{"args":["-sS","http://127.0.0.1:{port}/"]}""")]),
        ("tools-call-curl-path-outside-root", "none", [.. Handshake, Call(2, "curl", """{"args":["-sS","-o","{outside}/out.txt","http://127.0.0.1:1/"]}""")]),
        ("tools-call-curl-missing-args", "none", [.. Handshake, Call(2, "curl", "{}")]),
        ("tools-call-transfer-success", "serve", [.. Handshake, Call(2, "transfer", """{"url":"http://127.0.0.1:{port}/item","headers":[{"name":"X-Test","value":"yes"}]}""")]),
        ("tools-call-transfer-closed-port", "closed", [.. Handshake, Call(2, "transfer", """{"url":"http://127.0.0.1:{port}/"}""")]),
        ("tools-call-transfer-wrong-shape", "none", [.. Handshake, Call(2, "transfer", """{"url":"http://127.0.0.1:1/","headers":"X-Test: yes"}""")]),
        ("tools-call-transfer-missing-url", "none", [.. Handshake, Call(2, "transfer", "{}")]),
        ("tools-call-parse-url-success", "none", [.. Handshake, Call(2, "parse_url", """{"url":"https://user:pw@example.com:8443/a?x=1#f"}""")]),
        ("tools-call-parse-url-refused", "none", [.. Handshake, Call(2, "parse_url", """{"url":"http://"}""")]),
        ("tools-call-build-url-success", "none", [.. Handshake, Call(2, "build_url", """{"scheme":"https","host":"example.com","path":"/a","query":"x=1"}""")]),
        ("tools-call-build-url-refused", "none", [.. Handshake, Call(2, "build_url", """{"scheme":"https","port":"notaport","host":"example.com"}""")]),
        ("tools-call-unknown-tool", "none", [.. Handshake, Call(2, "no_such_tool", "{}")]),
        ("resources-list", "none", [.. Handshake, Rpc(2, "resources/list", "{}")]),
        ("resources-read", "none", [.. Handshake, Rpc(2, "resources/read", """{"uri":"curl://ai-help"}""")]),
        ("resources-read-missing", "none", [.. Handshake, Rpc(2, "resources/read", """{"uri":"curl://no-such-resource"}""")]),
        ("unknown-method", "none", [.. Handshake, Rpc(2, "no/such/method", "{}")]),
        ("request-before-initialize", "none", [Rpc(1, "tools/list", "{}")]),
        ("malformed-json", "none", [.. Handshake, "?{ this is not json"]),
        ("non-object-message", "none", [.. Handshake, "?42"]),
        ("batch-array", "none", [.. Handshake, "?[" + Rpc(2, "ping", "{}") + "," + Rpc(3, "ping", "{}") + "]"]),
    ];

    /// <summary>The conversation names, for the data-driven test.</summary>
    public static IEnumerable<string> ConversationNames => Conversations.Select(conversation => conversation.Name);

    [TestMethod]
    [DynamicData(nameof(ConversationNames))]
    public async Task Conversation_AnswersAsRecorded(string name)
    {
        (string _, string listenerKind, string[] lines) = Conversations.Single(conversation => conversation.Name == name);
        string root = Path.Combine(ResolveRealPath(Path.GetTempPath()), $"curl-mcp-golden-{Guid.NewGuid():N}");
        string outside = root + "-outside";
        Directory.CreateDirectory(root);
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task<string>? served = null;
        if (listenerKind == "serve")
        {
            served = LoopbackHttpResponder.ServeOneResponseAsync(listener, HelloResponse);
        }
        else if (listenerKind == "closed")
        {
            listener.Stop();
        }

        try
        {
            JsonArray exchanges = [];
            await using (McpStdioClient client = McpStdioClient.Start(root))
            {
                foreach (string template in lines)
                {
                    bool optional = template.StartsWith('?');
                    bool silent = template.StartsWith('!');
                    string bare = optional || silent ? template[1..] : template;
                    string request = bare
                        .Replace("{root}", JsonEscape(root), StringComparison.Ordinal)
                        .Replace("{outside}", JsonEscape(outside), StringComparison.Ordinal)
                        .Replace("{port}", port.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                    string? response = await client.ExchangeAsync(request, silent ? null : optional ? MaybeTimeout : ResponseTimeout);
                    exchanges.Add(new JsonObject
                    {
                        ["request"] = bare,
                        ["response"] = response is null ? null : Normalise(ParseOrText(response), root, outside, port),
                    });
                }
            }

            if (served is not null)
            {
                await served;
            }

            Assert.IsFalse(Directory.Exists(outside), "The conversation must write nothing outside the root.");
            JsonObject actual = new() { ["name"] = name, ["exchanges"] = exchanges };
            string file = Path.Combine(GoldenFolder, $"{name}.json");
            if (Environment.GetEnvironmentVariable("CURL_MCP_RECORD_GOLDEN") == "1")
            {
                Directory.CreateDirectory(GoldenFolder);
                await File.WriteAllTextAsync(file, actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
                return;
            }

            JsonNode golden = JsonNode.Parse(await File.ReadAllTextAsync(file))!;
            Assert.IsTrue(JsonNode.DeepEquals(golden, actual), $"Expected:\n{golden.ToJsonString()}\nActual:\n{actual.ToJsonString()}");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void GoldenFolder_HoldsOneFilePerConversation()
    {
        string[] files = [.. Directory.GetFiles(GoldenFolder, "*.json").Select(file => Path.GetFileNameWithoutExtension(file)).Order(StringComparer.Ordinal)];
        CollectionAssert.AreEqual(ConversationNames.Order(StringComparer.Ordinal).ToArray(), files);
    }

    [TestMethod]
    public void GoldenToolsList_NamesExactlyTheFiveTools()
    {
        JsonNode golden = JsonNode.Parse(File.ReadAllText(Path.Combine(GoldenFolder, "tools-list.json")))!;
        string[] names = [.. golden["exchanges"]![2]!["response"]!["result"]!["tools"]!.AsArray().Select(tool => tool!["name"]!.GetValue<string>()).Order(StringComparer.Ordinal)];
        CollectionAssert.AreEqual(new[] { "build_url", "curl", "parse_url", "transfer", "version" }, names);
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;

    private static string Rpc(int id, string method, string parameters) =>
        $$"""{"jsonrpc":"2.0","id":{{id}},"method":"{{method}}","params":{{parameters}}}""";

    private static string Call(int id, string tool, string arguments) =>
        Rpc(id, "tools/call", $$"""{"name":"{{tool}}","arguments":{{arguments}}}""");

    private static string Init(string version) =>
        $$$"""{"protocolVersion":"{{{version}}}","capabilities":{},"clientInfo":{"name":"golden","version":"1.0"}}""";

    private static string Notified() => """!{"jsonrpc":"2.0","method":"notifications/initialized"}""";

    private static string JsonEscape(string value) => JsonSerializer.Serialize(value)[1..^1];

    private static JsonNode? ParseOrText(string line)
    {
        try
        {
            return JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return JsonValue.Create(line);
        }
    }

    /// <summary>
    /// The path with every symbolic link along it resolved, so the root the test names matches the
    /// one the server reports (macOS's temporary folder sits under <c>/var</c>, a link to
    /// <c>/private/var</c>, BL-3415).
    /// </summary>
    private static string ResolveRealPath(string path)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        string? parent = Path.GetDirectoryName(full);
        if (parent is null)
        {
            return full;
        }

        string candidate = Path.Combine(ResolveRealPath(parent), Path.GetFileName(full));
        FileSystemInfo? target = new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true);
        return target is null ? candidate : ResolveRealPath(target.FullName);
    }

    private static JsonNode? Normalise(JsonNode? node, string root, string outside, int port)
    {
        switch (node)
        {
            case JsonObject obj:
                JsonObject result = [];
                foreach (KeyValuePair<string, JsonNode?> property in obj)
                {
                    result[property.Key] = IsTimingName(property.Key) && property.Value is JsonValue
                        ? "<TIMING>"
                        : PlatformNames.Contains(property.Key)
                            ? "<PLATFORM>"
                            : property.Key == ExtensionSchemesName
                                ? "<EXTENSION-SCHEMES>"
                                : Normalise(property.Value, root, outside, port);
                }

                if (result["content"] is JsonValue { } content && content.TryGetValue(out string? contentText) && contentText.Contains("<OS-ERROR>", StringComparison.Ordinal))
                {
                    result["totalBytes"] = "<LENGTH>";
                    result["returnedBytes"] = "<LENGTH>";
                }

                return result;
            case JsonArray array:
                return new JsonArray([.. array.Select(item => Normalise(item, root, outside, port))]);
            case JsonValue value when value.TryGetValue(out string? text):
                return NormaliseText(text, root, outside, port);
            default:
                return node?.DeepClone();
        }
    }

    private static JsonNode NormaliseText(string text, string root, string outside, int port)
    {
        if (text.StartsWith('{') && ParseOrText(text) is JsonObject embedded)
        {
            return Normalise(embedded, root, outside, port)!;
        }

        string replaced = text
            .Replace(outside, "<OUTSIDE>", StringComparison.Ordinal)
            .Replace(root, "<ROOT>", StringComparison.Ordinal)
            .Replace(outside.Replace('\\', '/'), "<OUTSIDE>", StringComparison.Ordinal)
            .Replace(root.Replace('\\', '/'), "<ROOT>", StringComparison.Ordinal)
            .Replace($"127.0.0.1:{port}", "127.0.0.1:<PORT>", StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\\', '/');
        return Regex.Replace(replaced, @"after \d+ ms: [^\n]*", "after <TIMING> ms: <OS-ERROR>");
    }

    private static readonly HashSet<string> PlatformNames = ["features", "tlsBackend", "versionLine", "primaryPort"];

    // The version tool's extension schemes grow with every extension that lands, so the golden holds a placeholder;
    // Curl.Mcp.UnitTests pins the list against ExtensionListing.Schemes (BL-4451, BL-4453).
    private const string ExtensionSchemesName = "extendedProtocols";

    private static bool IsTimingName(string name) =>
        name.StartsWith("elapsed", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("speed", StringComparison.Ordinal)
        || name.EndsWith("Seconds", StringComparison.Ordinal)
        || name.EndsWith("Milliseconds", StringComparison.Ordinal)
        || name.EndsWith("Time", StringComparison.Ordinal);
}
