using System.Globalization;

namespace Curl.Conformance.Differential.Libcurl.Multi;

/// <summary>
/// Replays one multi or share case against an <see cref="ILibcurlMultiTarget"/> and writes what it
/// returned as the tester's <c>LibTest</c> multi-mode and share-mode lines (ADR-0793). A step that
/// names an easy handle its case already cleaned up is written as <c>freed</c> without a call, as
/// passing a freed handle to the reference libcurl is undefined behaviour.
/// </summary>
/// <param name="target">The library the case runs against.</param>
/// <param name="time">The clock <c>pollms</c>, <c>waitms</c> and <c>runfor</c> are timed with.</param>
public sealed class LibcurlMultiReplayer(ILibcurlMultiTarget target, TimeProvider time)
{
    /// <summary>How many times <c>run</c> calls <c>curl_multi_perform</c> at most.</summary>
    private const int RunSpinLimit = 600;

    private static readonly Dictionary<string, Func<LibcurlMultiReplayer, LibcurlMultiStep, ValueTask<string>>> MultiSteps = new(StringComparer.Ordinal)
    {
        ["easy"] = (replayer, step) => Done(replayer.AddEasy(step, false)),
        ["add"] = (replayer, step) => Done(replayer.Live(step, "add", easy => replayer._target.AddHandle(easy).ToString(CultureInfo.InvariantCulture))),
        ["add_null"] = (replayer, _) => Done($" addnull={replayer._target.AddHandle(null)}"),
        ["remove"] = (replayer, step) => Done(replayer.Live(step, "rm", easy => replayer._target.RemoveHandle(easy).ToString(CultureInfo.InvariantCulture))),
        ["remove_null"] = (replayer, _) => Done($" rmnull={replayer._target.RemoveHandle(null)}"),
        ["setopt"] = (replayer, step) => Done($" set{step.Setting!.Id}={replayer._target.SetMultiOption(step.Setting)}"),
        ["perform"] = (replayer, _) => replayer.PerformAsync(),
        ["cleanup_easy"] = (replayer, step) => Done(replayer.Free(step, "")),
        ["run"] = (replayer, _) => replayer.RunAsync(),
        ["info"] = (replayer, _) => Done(replayer.Info()),
        ["bodies"] = (replayer, _) => Done(replayer.Bodies()),
        ["pollms"] = (replayer, step) => replayer.TimedAsync("poll", replayer._target.PollAsync, (int)step.Argument),
        ["waitms"] = (replayer, step) => replayer.TimedAsync("wait", replayer._target.WaitAsync, (int)step.Argument),
        ["runfor"] = (replayer, step) => replayer.RunForAsync((int)step.Argument),
        ["handles"] = (replayer, _) => Done($" nh={replayer._target.HandleCount()}"),
        ["cleanup_multi"] = (replayer, _) => replayer.CleanupMultiAsync(),
    };

    private static readonly Dictionary<string, Func<LibcurlMultiReplayer, LibcurlMultiStep, ValueTask<string>>> ShareSteps = new(StringComparer.Ordinal)
    {
        ["sset"] = (replayer, step) => Done($" sset{step.Setting!.Id}={replayer._target.SetShareOption(step.Setting)}"),
        ["ssetnull"] = (replayer, step) => Done($" ssetnull{step.Setting!.Id}={replayer._target.SetShareOption(step.Setting)}"),
        ["easy"] = (replayer, step) => Done(replayer.AddEasy(step, true)),
        ["perform"] = (replayer, step) => Done(replayer.Live(step, $"perf{step.Argument}", replayer.PerformEasy)),
        ["cleanup_easy"] = (replayer, step) => Done(replayer.Free(step, " ce")),
        ["cleanup_share"] = (replayer, _) => Done($" cs={replayer._target.CleanupShare()}"),
    };

    private readonly ILibcurlMultiTarget _target = target ?? throw new ArgumentNullException(nameof(target));
    private readonly TimeProvider _time = time ?? throw new ArgumentNullException(nameof(time));
    private readonly HashSet<int> _freed = [];
    private int _easyCount;

    /// <summary>Replays a multi-mode case as one line starting with its index, as <c>MultiMode</c> wrote it.</summary>
    /// <param name="index">The case's index in its file.</param>
    /// <param name="steps">The case's steps.</param>
    /// <returns>The line; an exception ends it with <c>EXC {type}</c>.</returns>
    public async Task<string> ReplayMultiAsync(int index, IReadOnlyList<LibcurlMultiStep> steps) =>
        index.ToString(CultureInfo.InvariantCulture) + await ReplayAsync(MultiSteps, steps);

    /// <summary>Replays a share-mode case as one line, as <c>ShareMode</c> wrote it.</summary>
    /// <param name="steps">The case's steps.</param>
    /// <returns>The line, trimmed; an exception ends it with <c>EXC {type}</c>.</returns>
    public async Task<string> ReplayShareAsync(IReadOnlyList<LibcurlMultiStep> steps) => (await ReplayAsync(ShareSteps, steps)).Trim();

    private async Task<string> ReplayAsync(Dictionary<string, Func<LibcurlMultiReplayer, LibcurlMultiStep, ValueTask<string>>> handlers, IReadOnlyList<LibcurlMultiStep> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var line = "";
        try
        {
            foreach (var step in steps)
            {
                line += handlers.TryGetValue(step.Operation, out var handler) ? await handler(this, step) : "";
            }
        }
        catch (Exception exception)
        {
            line += $" EXC {exception.GetType().Name}";
        }

        return line;
    }

    private static ValueTask<string> Done(string token) => ValueTask.FromResult(token);

    private string AddEasy(LibcurlMultiStep step, bool writesFailures)
    {
        var codes = _target.AddEasy(step.Options);
        _easyCount++;
        var failures = "";
        for (var index = 0; writesFailures && index < codes.Count; index++)
        {
            failures += codes[index] == 0 ? "" : $" eset{step.Options[index].Id}={codes[index]}";
        }

        return failures;
    }

    private string Live(LibcurlMultiStep step, string name, Func<int, string> call)
    {
        var easy = (int)step.Argument;
        return $" {name}={(_freed.Contains(easy) ? "freed" : call(easy))}";
    }

    private string Free(LibcurlMultiStep step, string token)
    {
        var easy = (int)step.Argument;
        if (_freed.Add(easy))
        {
            _target.CleanupEasy(easy);
        }

        return token;
    }

    private string PerformEasy(int easy)
    {
        var (code, responseCode, connects) = _target.PerformEasy(easy);
        return $"{code}/{responseCode}/nc{connects}";
    }

    private async ValueTask<string> PerformAsync()
    {
        var (code, running) = await _target.PerformAsync();
        return $" perf={code}/{running}";
    }

    private async ValueTask<string> RunAsync()
    {
        int running = 1, spins = 0, last = 0;
        while (running > 0 && spins++ < RunSpinLimit)
        {
            (last, running) = await _target.PerformAsync();
            if (running > 0)
            {
                await _target.PollAsync(50);
            }
        }

        return $" run={last}/{running}";
    }

    private async ValueTask<string> RunForAsync(int milliseconds)
    {
        var started = _time.GetTimestamp();
        int running = 1, calls = 0;
        while (running > 0 && _time.GetElapsedTime(started).TotalMilliseconds < milliseconds)
        {
            (_, running) = await _target.PerformAsync();
            calls++;
            if (running > 0)
            {
                await _target.PollAsync(100);
            }
        }

        return $" runfor={running}/{(long)_time.GetElapsedTime(started).TotalMilliseconds / 250}/{(calls > 200 ? "SPIN" : "ok")}";
    }

    private async ValueTask<string> TimedAsync(string name, Func<int, ValueTask<(int Code, int Ready)>> call, int milliseconds)
    {
        var started = _time.GetTimestamp();
        var (code, ready) = await call(milliseconds);
        return $" {name}={code}/{ready}/{(long)_time.GetElapsedTime(started).TotalMilliseconds / 200}";
    }

    private string Info()
    {
        var messages = _target.ReadMessages().Select(m => $"{m.Type}:{m.Easy}:{m.Result}:q{m.Queue}").Order(StringComparer.Ordinal);
        return $" info=[{string.Join(',', messages)}]";
    }

    private string Bodies()
    {
        var bodies = "";
        for (var easy = 0; easy < _easyCount; easy++)
        {
            bodies += _freed.Contains(easy) ? $" b{easy}=freed" : Body(easy);
        }

        return bodies;
    }

    private string Body(int easy)
    {
        var (body, responseCode) = _target.Body(easy);
        return $" b{easy}={Convert.ToHexString(body)}/{responseCode}";
    }

    private async ValueTask<string> CleanupMultiAsync()
    {
        await _target.CleanupMultiAsync();
        return " cleaned";
    }
}
