using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Conformance.Extended.McpClient.Stdio;

/// <summary>
/// The stand-in for an MCP server Curl starts for <c>mcp+stdio://</c>: it reads newline-delimited JSON-RPC from its
/// standard input, answers each request through <see cref="McpReferenceToolServer" /> on its standard output, logs one
/// line per message to its own standard error, and exits when its standard input is closed. Everything runs in memory
/// over two <see cref="Pipe" />s, so no real process starts (BL-4483).
/// </summary>
public sealed class McpStdioReferenceProcess : IExtensionProcess
{
    /// <summary>The exit code of a server that exits before answering a message, or is killed.</summary>
    public const int ExitedEarlyCode = 1;

    private readonly McpReferenceToolServer server;
    private readonly McpStdioServerBehaviour behaviour;
    private readonly Pipe input = new();
    private readonly Pipe output = new();
    private readonly MemoryStream received = new();
    private readonly List<string> standardErrorLines = [];
    private readonly TaskCompletionSource<int> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource killed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private volatile bool exitedEarly;
    private int messages;

    /// <summary>Starts the stand-in for <paramref name="program" /> with <paramref name="arguments" />.</summary>
    /// <param name="server">The JSON-RPC side that answers each request.</param>
    /// <param name="behaviour">How the server misbehaves, if at all.</param>
    /// <param name="program">The program name Curl started.</param>
    /// <param name="arguments">The arguments Curl started it with.</param>
    public McpStdioReferenceProcess(McpReferenceToolServer server, McpStdioServerBehaviour behaviour, string program, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(behaviour);
        this.server = server;
        this.behaviour = behaviour;
        Program = program;
        Arguments = [.. arguments];
        StandardInput = input.Writer.AsStream();
        StandardOutput = output.Reader.AsStream();
        _ = ServeAsync();
    }

    /// <summary>Gets the program name Curl started.</summary>
    public string Program { get; }

    /// <summary>Gets the arguments Curl started the program with.</summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <inheritdoc />
    public Stream StandardInput { get; }

    /// <inheritdoc />
    public Stream StandardOutput { get; }

    /// <summary>Gets every whole line the server read from its standard input before it exited, each with its newline.</summary>
    public byte[] StandardInputBytes => received.ToArray();

    /// <summary>Gets the lines the server logged to its own standard error.</summary>
    public IReadOnlyList<string> StandardErrorLines => standardErrorLines;

    /// <summary>Gets a value indicating whether the server saw its standard input closed.</summary>
    public bool StandardInputClosed { get; private set; }

    /// <summary>Gets a value indicating whether Curl killed the server.</summary>
    public bool Killed { get; private set; }

    /// <inheritdoc />
    public Task<int> WaitForExitAsync(CancellationToken cancellationToken) => exited.Task.WaitAsync(cancellationToken);

    /// <inheritdoc />
    public void Kill()
    {
        Killed = true;
        exitedEarly = true;
        input.Reader.CancelPendingRead();
        killed.TrySetResult();
    }

    /// <summary>Releases nothing: the pipes hold only memory, and the server stops when its input closes or it is killed.</summary>
    public void Dispose()
    {
    }

    // Answers each line until the input closes, the server exits early or it is killed, then exits.
    private async Task ServeAsync()
    {
        ReadResult read;
        do
        {
            read = await input.Reader.ReadAsync().ConfigureAwait(false);
            input.Reader.AdvanceTo(AnswerEachLine(read.Buffer), read.Buffer.End);
            await output.Writer.FlushAsync().ConfigureAwait(false);
        }
        while (!read.IsCompleted && !exitedEarly);

        StandardInputClosed = read.IsCompleted;
        if (StandardInputClosed && behaviour.IgnoresClosedStandardInput)
        {
            await killed.Task.ConfigureAwait(false);
        }

        await input.Reader.CompleteAsync().ConfigureAwait(false);
        await output.Writer.CompleteAsync().ConfigureAwait(false);
        exited.SetResult(exitedEarly ? ExitedEarlyCode : 0);
    }

    // Answers every whole line in buffer and returns where the unread rest starts.
    private SequencePosition AnswerEachLine(ReadOnlySequence<byte> buffer)
    {
        SequenceReader<byte> reader = new(buffer);
        while (!exitedEarly && reader.TryReadTo(out ReadOnlySequence<byte> line, (byte)'\n'))
        {
            AnswerLine(line.ToArray());
        }

        return reader.Position;
    }

    private void AnswerLine(byte[] line)
    {
        received.Write(line);
        received.WriteByte((byte)'\n');
        messages++;
        standardErrorLines.Add($"reference-mcp-server: received message {messages}");
        if (messages == behaviour.ExitBeforeAnsweringMessage)
        {
            exitedEarly = true;
            return;
        }

        if (server.Answer(Encoding.UTF8.GetString(line)) is { } answer)
        {
            output.Writer.Write(Encoding.UTF8.GetBytes(behaviour.LineBeforeEachAnswer + answer + "\n"));
        }
    }
}
