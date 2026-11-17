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
    private readonly TimeSpan? maxTime;
    private readonly long operationStarted;
    private bool ended;

    /// <summary>Creates a session over the started <paramref name="process"/>.</summary>
    /// <param name="process">The started server.</param>
    /// <param name="timeProvider">The clock the grace period and <paramref name="maxTime"/> are counted on.</param>
    /// <param name="maxTime">The <c>-m</c> limit on the whole operation, or <see langword="null"/> for none.</param>
    /// <param name="operationStarted">The <paramref name="timeProvider"/> timestamp the operation began at, or <see langword="null"/> for now.</param>
    public McpStdioSession(IMcpServerProcess process, TimeProvider timeProvider, TimeSpan? maxTime = null, long? operationStarted = null)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.process = process;
        this.timeProvider = timeProvider;
        this.maxTime = maxTime;
        this.operationStarted = operationStarted ?? timeProvider.GetTimestamp();
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
    /// waits for it to exit for <see cref="ExitGracePeriod"/> or the time left on <c>-m</c>,
    /// whichever is shorter, and kills it if it has not. Ending a session twice does nothing the second time.
    /// </summary>
    public Task EndAsync() => EndWithGraceAsync(GraceLeft());

    /// <summary>
    /// Ends the session: after a normal <see cref="EndAsync()"/> it does nothing more; a session
    /// disposed without one (the transfer failed or was cancelled) closes the server's standard
    /// input and kills the server at once, without the grace period.
    /// </summary>
    public async ValueTask DisposeAsync() => await EndWithGraceAsync(TimeSpan.Zero).ConfigureAwait(false);

    private TimeSpan GraceLeft()
    {
        if (maxTime is not { } limit)
        {
            return ExitGracePeriod;
        }

        var left = limit - timeProvider.GetElapsedTime(operationStarted);
        return left <= TimeSpan.Zero ? TimeSpan.Zero : left < ExitGracePeriod ? left : ExitGracePeriod;
    }

    private async Task EndWithGraceAsync(TimeSpan grace)
    {
        if (ended)
        {
            return;
        }

        ended = true;
        process.StandardInput.Dispose();
        using var graceSource = new CancellationTokenSource(grace, timeProvider);
        try
        {
            await process.WaitForExitAsync(graceSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
        }

        process.Dispose();
    }
}
