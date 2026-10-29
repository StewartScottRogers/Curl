namespace Curl.Extension.WebSocketRepl;

/// <summary>
/// Gives its lines, then either ends the input or, like a terminal nobody types into, waits until cancelled.
/// </summary>
internal sealed class ScriptedLineReader(bool endsAfterLines, params string[] lines) : TextReader
{
    private int next;

    public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (next < lines.Length)
        {
            return lines[next++];
        }

        if (!endsAfterLines)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return null;
    }
}
