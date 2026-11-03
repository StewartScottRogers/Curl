using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.Plugin;

/// <summary>
/// A stand-in <c>curl-proto-&lt;scheme&gt;</c> helper (ADR-1142): it records every byte Curl writes to its standard input
/// and answers on its standard output with the bytes it was given, then exits with the status it was given. No process starts.
/// </summary>
public sealed class PluginReferenceHelper : IExtensionProcess
{
    private readonly RecordingInput input;
    private readonly int exitStatus;

    /// <summary>Creates a helper that writes <paramref name="output" /> whatever Curl sends it.</summary>
    /// <param name="output">Everything the helper writes to its standard output.</param>
    /// <param name="exitStatus">The status the helper exits with.</param>
    /// <param name="inputBreaks">Whether writing to the helper's standard input fails as a broken pipe does.</param>
    public PluginReferenceHelper(byte[] output, int exitStatus = 0, bool inputBreaks = false)
    {
        input = new RecordingInput(inputBreaks);
        this.exitStatus = exitStatus;
        StandardOutput = new MemoryStream(output, writable: false);
    }

    /// <summary>
    /// Creates a helper that keeps the contract: it lists <paramref name="capabilities" /> one per line and an empty
    /// line, then writes <paramref name="reply" /> (<c>ok</c> or <c>error &lt;message&gt;</c>) and <paramref name="body" />.
    /// </summary>
    /// <param name="capabilities">The capabilities it lists, a mandatory one starting with <c>*</c>.</param>
    /// <param name="reply">Its reply line to the command.</param>
    /// <param name="body">What follows the reply.</param>
    /// <param name="exitStatus">The status it exits with.</param>
    /// <param name="lineEnd">What ends each line: LF, or CRLF as a Windows batch file's <c>echo</c>.</param>
    /// <returns>The helper.</returns>
    public static PluginReferenceHelper Answering(IReadOnlyList<string> capabilities, string reply, string body = "", int exitStatus = 0, string lineEnd = "\n") =>
        new(Encoding.UTF8.GetBytes(string.Concat(capabilities.Select(capability => capability + lineEnd)) + lineEnd + reply + lineEnd + body), exitStatus);

    /// <summary>Gets every byte Curl wrote to the helper's standard input.</summary>
    public byte[] Received => input.ToArray();

    /// <inheritdoc />
    public Stream StandardInput => input;

    /// <inheritdoc />
    public Stream StandardOutput { get; }

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => Task.FromResult(exitStatus);

    /// <summary>Does nothing: the stand-in has already exited.</summary>
    public void Kill()
    {
    }

    /// <summary>Does nothing: <see cref="Received" /> stays readable after Curl closes the helper.</summary>
    public void Dispose()
    {
    }

    // Keeps what Curl writes, or refuses it as a pipe whose reader has gone.
    private sealed class RecordingInput(bool breaks) : MemoryStream
    {
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            breaks ? ValueTask.FromException(new IOException("The pipe has been ended.")) : base.WriteAsync(buffer, cancellationToken);
    }
}
