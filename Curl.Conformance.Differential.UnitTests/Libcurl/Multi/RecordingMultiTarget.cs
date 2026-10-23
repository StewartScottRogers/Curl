using Curl.Conformance.Differential.Libcurl.Multi;

namespace Curl.Conformance.Differential.UnitTests.Libcurl.Multi;

/// <summary>An <see cref="ILibcurlMultiTarget"/> that answers fixed codes, records its calls and moves <see cref="Clock"/> on each poll or wait.</summary>
internal sealed class RecordingMultiTarget : ILibcurlMultiTarget
{
    public SteppedTimeProvider Clock { get; } = new();

    public List<string> Calls { get; } = [];

    public Queue<int> Running { get; } = new();

    public int DefaultRunning { get; set; }

    public int[] SetCodes { get; set; } = [];

    public IReadOnlyList<int> AddEasy(IReadOnlyList<LibcurlMultiValue> options)
    {
        Calls.Add($"easy{options.Count}");
        return SetCodes;
    }

    public int AddHandle(int? easy) => easy is null ? 2 : 0;

    public int RemoveHandle(int? easy) => easy is null ? 2 : 0;

    public int SetMultiOption(LibcurlMultiValue setting) => setting.Id == 99999 ? 6 : 0;

    public ValueTask<(int Code, int Running)> PerformAsync() => ValueTask.FromResult((0, Running.Count > 0 ? Running.Dequeue() : DefaultRunning));

    public ValueTask<(int Code, int Ready)> PollAsync(int timeoutMilliseconds)
    {
        Clock.Advance(timeoutMilliseconds);
        return ValueTask.FromResult((0, 0));
    }

    public ValueTask<(int Code, int Ready)> WaitAsync(int timeoutMilliseconds)
    {
        Clock.Advance(timeoutMilliseconds);
        return ValueTask.FromResult((0, 1));
    }

    public IReadOnlyList<LibcurlMultiMessage> ReadMessages() => [new(1, 1, 0, 0), new(1, 0, 7, 1)];

    public (byte[] Body, long ResponseCode) Body(int easy) => ([0x6F, 0x6B], 200);

    public int HandleCount() => 3;

    public void CleanupEasy(int easy) => Calls.Add($"cleanup{easy}");

    public ValueTask CleanupMultiAsync()
    {
        Calls.Add("cleanup_multi");
        return ValueTask.CompletedTask;
    }

    public int SetShareOption(LibcurlMultiValue setting) => setting.Kind == "null" ? 3 : 0;

    public (int Code, long ResponseCode, long Connects) PerformEasy(int easy) => (0, 200, 1);

    public int CleanupShare() => 0;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A clock that moves only when told to.</summary>
internal sealed class SteppedTimeProvider : TimeProvider
{
    private long _milliseconds;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _milliseconds;

    public void Advance(int milliseconds) => _milliseconds += milliseconds;
}
