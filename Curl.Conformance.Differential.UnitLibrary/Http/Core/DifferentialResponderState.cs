namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// The responder's per-run state, shared by every connection: server.py's request log (<c>LOG</c>, the
/// raw head plus body of each request) and its counters (<c>STATE</c>), both cleared by <see cref="Reset"/>.
/// </summary>
public sealed class DifferentialResponderState
{
    private static readonly byte[] LogSeparator = "\0SEP\0"u8.ToArray();

    private readonly Lock _lock = new();
    private readonly List<byte[]> _requestLog = [];
    private readonly Dictionary<string, int> _counters = new(StringComparer.Ordinal);

    /// <summary>Adds one request's raw head plus body to the request log.</summary>
    public void LogRequest(byte[] loggedBytes)
    {
        lock (_lock)
        {
            _requestLog.Add(loggedBytes);
        }
    }

    /// <summary>Adds one to the counter <paramref name="name"/> (zero before its first use) and returns its new value.</summary>
    public int IncrementCounter(string name)
    {
        lock (_lock)
        {
            var value = _counters.GetValueOrDefault(name) + 1;
            _counters[name] = value;
            return value;
        }
    }

    /// <summary>Clears the request log and every counter.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _requestLog.Clear();
            _counters.Clear();
        }
    }

    /// <summary>Returns the request log joined by <c>\0SEP\0</c> and clears it; the counters are kept.</summary>
    public byte[] TakeRequestLog()
    {
        lock (_lock)
        {
            using var joined = new MemoryStream();
            for (var index = 0; index < _requestLog.Count; index++)
            {
                if (index > 0)
                {
                    joined.Write(LogSeparator);
                }

                joined.Write(_requestLog[index]);
            }

            _requestLog.Clear();
            return joined.ToArray();
        }
    }
}
