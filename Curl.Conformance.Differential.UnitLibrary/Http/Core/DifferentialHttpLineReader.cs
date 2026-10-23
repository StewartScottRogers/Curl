namespace Curl.Conformance.Differential.Http.Core;

/// <summary>
/// Reads a request stream the way server.py's buffered <c>rfile</c> does: whole lines with their
/// terminator, and counted byte runs, each cut short only by the end of the stream.
/// </summary>
public sealed class DifferentialHttpLineReader(Stream stream)
{
    private readonly byte[] _buffer = new byte[8192];
    private int _start;
    private int _end;

    /// <summary>Reads up to and including the next <c>\n</c>; an empty array means the stream has ended.</summary>
    public async Task<byte[]> ReadLineAsync(CancellationToken cancellationToken)
    {
        using var line = new MemoryStream();
        while (_start < _end || await FillAsync(cancellationToken))
        {
            var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
            var stop = newline < 0 ? _end : newline + 1;
            line.Write(_buffer, _start, stop - _start);
            _start = stop;
            if (newline >= 0)
            {
                break;
            }
        }

        return line.ToArray();
    }

    /// <summary>Reads <paramref name="count"/> bytes, or every byte left when <paramref name="count"/> is negative.</summary>
    public async Task<byte[]> ReadAsync(long count, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        while ((count < 0 || output.Length < count) && (_start < _end || await FillAsync(cancellationToken)))
        {
            var available = _end - _start;
            var take = count < 0 ? available : (int)Math.Min(available, count - output.Length);
            output.Write(_buffer, _start, take);
            _start += take;
        }

        return output.ToArray();
    }

    private async Task<bool> FillAsync(CancellationToken cancellationToken)
    {
        _start = 0;
        _end = await stream.ReadAsync(_buffer, cancellationToken);
        return _end > 0;
    }
}
