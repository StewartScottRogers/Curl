using System.Text;
using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Runs one route through the responder over an in-memory connection on a clock that never sleeps.</summary>
internal static class BodyRouteResponder
{
    /// <summary>The <c>Date</c> line every <c>resp</c> answer carries.</summary>
    public const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    /// <summary>Sends <paramref name="requests"/> to a responder holding only <paramref name="route"/> and returns what it wrote, as Latin-1.</summary>
    public static async Task<string> RespondAsync(IDifferentialRoute route, string requests) =>
        (await RespondWithDelaysAsync(route, requests)).Written;

    /// <summary>As <see cref="RespondAsync"/>, also returning each delay the route waited and how many bytes were written before it.</summary>
    public static async Task<(string Written, IReadOnlyList<(TimeSpan Delay, int BytesWrittenBefore)> Delays)> RespondWithDelaysAsync(
        IDifferentialRoute route, string requests)
    {
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(requests));
        var clock = new ImmediateTimeProvider(() => connection.Written.Length);
        var responder = new DifferentialHttpResponder([route], new DifferentialResponderState(), new DifferentialResponderOptions(18080, clock));
        await responder.RespondAsync(connection, CancellationToken.None);
        return (Encoding.Latin1.GetString(connection.Written), clock.Delays);
    }

    /// <summary>A clock whose timers fire at once on the thread pool, recording each delay asked for.</summary>
    private sealed class ImmediateTimeProvider(Func<int> bytesWritten) : TimeProvider
    {
        public List<(TimeSpan Delay, int BytesWrittenBefore)> Delays { get; } = [];

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Delays.Add((dueTime, bytesWritten()));
            ThreadPool.QueueUserWorkItem(_ => callback(state));
            return new FiredTimer();
        }
    }

    private sealed class FiredTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => false;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
