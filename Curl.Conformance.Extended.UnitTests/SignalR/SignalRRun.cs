using System.Text;
using System.Text.Json;
using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.SignalR;
using Curl.Console;

namespace Curl.Conformance.Extended.UnitTests.SignalR;

/// <summary>Runs curl against a reference SignalR hub and holds what the SignalR cases share.</summary>
internal static class SignalRRun
{
    public const int Port = 5000;

    public const string Url = "signalr://localhost:5000/hub";

    public const string Call = """{"type":1,"target":"Add","arguments":[1,2]}""";

    public const string StreamCall = """{"type":4,"target":"Count","arguments":[3]}""";

    public static readonly KnownFailures KnownFailures = KnownFailures.Load(Path.Combine(AppContext.BaseDirectory, "KnownFailures.txt"));

    public static readonly string Version0 = """{"connectionId":"conn-0","availableTransports":[{"transport":"WebSockets","transferFormats":["Text","Binary"]}]}""";

    public static string Version1(string token = "token-1") =>
        $$"""{"negotiateVersion":1,"connectionId":"conn-1","connectionToken":"{{token}}","availableTransports":[{"transport":"WebSockets","transferFormats":["Text","Binary"]}]}""";

    /// <summary>A hub that answers negotiate with version 1 and plays the handshake, the call and then <paramref name="replies" />.</summary>
    public static SignalRReferenceHub CallHub(params SignalRHubStep[] replies) =>
        new([Version1()], [SignalRHubStep.Receive, SignalRHubStep.Records("{}"), SignalRHubStep.Receive, .. replies]);

    public static Task<CurlRunResult> RunAsync(SignalRReferenceHub hub, params string[] arguments) =>
        ExtendedConformanceHarness.RunAsync(InProcessCurl.RunAsync, [ReferenceRoute.Tcp(Port, hub)], ["-sS", "-m", "30", .. arguments]);

    public static string Text(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    /// <summary>curl's standard error with each line ended by \n, as on every platform but Windows.</summary>
    public static string Error(CurlRunResult result) => Text(result.StandardError).ReplaceLineEndings("\n");

    /// <summary>Parses one record curl sent, checking it ends with the record separator.</summary>
    public static JsonDocument Message(string record)
    {
        Assert.EndsWith("\u001e", record);
        return JsonDocument.Parse(record[..^1]);
    }
}
