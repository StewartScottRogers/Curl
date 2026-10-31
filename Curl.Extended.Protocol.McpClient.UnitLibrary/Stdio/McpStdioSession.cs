using Curl.Extended.Protocol.McpClient.Wire;

namespace Curl.Extended.Protocol.McpClient.Stdio;

/// <summary>
/// One MCP session over the stdio transport (Model Context Protocol 2025-06-18, "Base Protocol &gt;
/// Transports &gt; stdio"): each message goes to the server's standard input as one line, and each
/// response is read line by line from its standard output.
/// </summary>
public sealed class McpStdioSession : IAsyncDisposable
{
    /// <summary>How long <see cref="EndAsync"/> waits for the server to exit after its standard input closes before it kills it.</summary>
    public static readonly TimeSpan ExitGracePeriod = TimeSpan.FromSeconds(5);

    private static readonly byte[] Newline = [(byte)'\n'];

    private readonly IMcpServerProcess process;
    private readonly TimeProvider timeProvider;
    private readonly McpLineReader lineReader;
    private bool ended;

    /// <summary>Creates a session over the started <paramref name="process"/>.</summary>
    public McpStdioSession(IMcpServerProcess process, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.process = process;
        this.timeProvider = timeProvider;
        lineReader = new McpLineReader(process.StandardOutput);
    }

    /// <summary>Writes <paramref name="message"/>, such as a notification, as one line on the server's standard input.</summary>
    /// <exception cref="ArgumentException"><paramref name="message"/> holds a line break, which the stdio transport forbids.</exception>
    public async Task SendMessageAsync(byte[] message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (message.AsSpan().IndexOfAny((byte)'\n', (byte)'\r') >= 0)
        {
            throw new ArgumentException("A stdio message must not contain embedded newlines.", nameof(message));
        }

        var input = process.StandardInput;
        await input.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        await input.WriteAsync(Newline, cancellationToken).ConfigureAwait(false);
        await input.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes <paramref name="request"/> as one line and returns the response to it, reading
    /// standard output line by line and skipping empty lines, server notifications, server requests
    /// and responses to other ids.
    /// </summary>
    /// <exception cref="McpServerExitedException">Standard output ended before the response came.</exception>
    /// <exception cref="McpWireFormatException">A line is not a JSON-RPC 2.0 message, or the response is malformed.</exception>
    public async Task<McpResponse> SendRequestAsync(McpOutgoingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await SendMessageAsync(request.Bytes, cancellationToken).ConfigureAwait(false);
        while (true)
        {
            var line = await lineReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new McpServerExitedException($"The MCP server exited before it answered request {request.Id}.");
            var response = line.Length == 0 ? null : McpResponseReader.ReadResponse(line, request.Id);
            if (response is not null)
            {
                return response;
            }
        }
    }

    /// <summary>
    /// Ends the session ("Lifecycle &gt; Shutdown &gt; stdio"): closes the server's standard input,
    /// waits up to <see cref="ExitGracePeriod"/> for it to exit, and kills it if it has not.
    /// Ending a session twice does nothing the second time.
    /// </summary>
    public async Task EndAsync()
    {
        if (ended)
        {
            return;
        }

        ended = true;
        process.StandardInput.Dispose();
        using var grace = new CancellationTokenSource(ExitGracePeriod, timeProvider);
        try
        {
            await process.WaitForExitAsync(grace.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
        }

        process.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await EndAsync().ConfigureAwait(false);
}
