using System.Text.Json;

namespace Curl.Mcp.Protocol.Server;

/// <summary>A method handler that runs the function a test gives it.</summary>
internal class StubMcpMethodHandler(string method, Func<JsonElement?, CancellationToken, Task<JsonElement>> handle) : IMcpMethodHandler
{
    public string Method => method;

    public async ValueTask<JsonElement> HandleAsync(JsonElement? parameters, CancellationToken cancellationToken) => await handle(parameters, cancellationToken);

    /// <summary>A handler that signals <paramref name="started" />, then waits until its request is cancelled and records that in <paramref name="cancelled" />.</summary>
    public static StubMcpMethodHandler Blocking(string method, TaskCompletionSource started, TaskCompletionSource cancelled) =>
        new(method, async (_, cancellationToken) =>
        {
            started.SetResult();
            await using (cancellationToken.Register(cancelled.SetResult))
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return default;
        });

    public static StubMcpMethodHandler Throwing(string method, Exception exception) => new(method, (_, _) => throw exception);
}
