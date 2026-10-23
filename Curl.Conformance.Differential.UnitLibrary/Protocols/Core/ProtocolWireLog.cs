using System.Text;

namespace Curl.Conformance.Differential.Protocols.Core;

/// <summary>
/// The one wire log every protocol server writes the lines it receives to (proto_servers.py's <c>LOG</c>,
/// shared because cases run one at a time); <see cref="Take"/> joins and clears it, <see cref="Reset"/> clears it.
/// </summary>
public sealed class ProtocolWireLog
{
    private readonly Lock _lock = new();
    private readonly List<string> _lines = [];

    /// <summary>Adds one line.</summary>
    public void Add(string line)
    {
        lock (_lock)
        {
            _lines.Add(line);
        }
    }

    /// <summary>Clears the log.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _lines.Clear();
        }
    }

    /// <summary>Returns the lines joined by <c>\n</c> as UTF-8 and clears the log.</summary>
    public byte[] Take()
    {
        lock (_lock)
        {
            var joined = Encoding.UTF8.GetBytes(string.Join('\n', _lines));
            _lines.Clear();
            return joined;
        }
    }
}
