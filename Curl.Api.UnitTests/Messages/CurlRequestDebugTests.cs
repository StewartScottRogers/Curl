using System.Text;
using Curl.Api.Callbacks;

namespace Curl.Api.Messages;

[TestClass]
public sealed class CurlRequestDebugTests
{
    private static async Task<ScriptedEasyTransfer> SendAsync(CurlRequest request)
    {
        ScriptedEasyTransfer easy = new() { BodyBytes = [1] };
        easy.HeaderLines.AddRange(["HTTP/1.1 200 OK\r\n", "\r\n"]);
        await using CurlClient client = new(new ScriptedCurlEngine(easy));
        await client.SendAsync(request, CancellationToken.None);
        return easy;
    }

    [TestMethod]
    public async Task SendAsync_WithDebug_IsVerboseAndPassesTheCallbackToTheEngine()
    {
        List<string> seen = [];
        DebugCallback debug = (type, data) =>
        {
            seen.Add($"{type}:{Encoding.ASCII.GetString(data)}");
            return 0;
        };

        ScriptedEasyTransfer easy = await SendAsync(new(HttpMethod.Get, "smtp://example/") { Debug = debug });

        Assert.IsTrue(easy.PerformedOptions!.Behaviour.Verbose);
        Assert.AreSame(debug, easy.PerformedOptions.Callbacks.Debug);
        easy.PerformedOptions.Callbacks.Debug!(CurlInfoType.HeaderIn, "550 5.1.1 no such user\r\n"u8);
        easy.PerformedOptions.Callbacks.Debug(CurlInfoType.HeaderIn, "250 2.1.5 ok\r\n"u8);
        CollectionAssert.AreEqual(new[] { "HeaderIn:550 5.1.1 no such user\r\n", "HeaderIn:250 2.1.5 ok\r\n" }, seen);
    }

    [TestMethod]
    public async Task SendAsync_WithoutDebug_IsNotVerboseAndSetsNoCallback()
    {
        ScriptedEasyTransfer easy = await SendAsync(new(HttpMethod.Get, "smtp://example/"));

        Assert.IsFalse(easy.PerformedOptions!.Behaviour.Verbose);
        Assert.IsNull(easy.PerformedOptions.Callbacks.Debug);
    }

    [TestMethod]
    public async Task SendAsync_WithDebug_DoesNotAddTheSecretsVerboseHides()
    {
        ScriptedEasyTransfer easy = await SendAsync(new(HttpMethod.Get, "http://u:secret@example/") { Debug = (_, _) => 0 });

        // The callback is the engine's own verbose event sink, the one curl -v writes through (ADR-1341):
        // Curl.Api adds no second source, so it hands over nothing -v would not print.
        Assert.IsNotNull(easy.PerformedOptions!.Callbacks.Debug);
        Assert.IsTrue(easy.PerformedOptions.Behaviour.Verbose);
    }
}
