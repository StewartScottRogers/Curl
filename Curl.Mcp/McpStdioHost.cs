using System.Globalization;
using System.Runtime.InteropServices;
using Curl.Mcp.Protocol.Server;
using Curl.Mcp.Protocol.Transport;

namespace Curl.Mcp;

/// <summary>
/// Hosts the MCP server over standard input and output without a generic host (BL-3382): reads
/// <c>CURL_MCP_ROOT</c> and <c>CURL_MCP_MAX_RESPONSE_BYTES</c> before any input, so a bad value stops the server
/// before it starts, makes the root the current directory (ADR-0575), builds the method handlers for that
/// configuration, and runs an <see cref="McpServer" /> on a <see cref="StdioMcpTransport" /> until the input ends,
/// the caller's token is cancelled or the process receives SIGINT (Ctrl+C), SIGQUIT, SIGTERM or SIGHUP.
/// Requests still in flight then have <see cref="ShutdownTimeout" /> to finish or cancel. Log lines go only to
/// the <see cref="McpStandardErrorLog" />; the output stream carries only protocol messages. One host runs once.
/// </summary>
internal sealed class McpStdioHost : IDisposable
{
    /// <summary>
    /// How long requests still in flight have to finish or cancel once a stop is requested: 30 seconds, the
    /// generic host's default shutdown timeout that this host replaces.
    /// </summary>
    internal static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(30);

    // The signals the generic host's console lifetime stopped on, and SIGHUP, sent when the terminal closes.
    private static readonly PosixSignal[] StopSignals = [PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM, PosixSignal.SIGHUP];

    private readonly McpServerInfo serverInfo;

    private readonly Func<McpConfinementRoot, McpResponseCap, IEnumerable<IMcpMethodHandler>> createHandlers;

    private readonly Stream input;

    private readonly Stream output;

    private readonly McpStandardErrorLog log;

    private readonly Func<string, string?> readEnvironmentVariable;

    private readonly TimeProvider timeProvider;

    private readonly Func<Action<PosixSignalContext>, IDisposable> registerStopSignals;

    private readonly CancellationTokenSource stopRequested = new();

    /// <summary>Initializes a new instance of the <see cref="McpStdioHost" /> class.</summary>
    /// <param name="serverInfo">The name and version <c>initialize</c> answers with.</param>
    /// <param name="createHandlers">Builds the handlers of every method beyond the lifecycle ones from the configuration read from the environment.</param>
    /// <param name="input">The stream the client's messages are read from, standard input in the process; the caller keeps ownership.</param>
    /// <param name="output">The stream the protocol messages are written to, standard output in the process; the caller keeps ownership.</param>
    /// <param name="log">The log every line about the host goes to.</param>
    /// <param name="readEnvironmentVariable">Reads an environment variable, <see langword="null" /> when it is unset.</param>
    /// <param name="timeProvider">The clock <see cref="ShutdownTimeout" /> is measured on.</param>
    /// <param name="registerStopSignals">Registers the given handler for the stop signals and returns what unregisters it; <see cref="RegisterProcessStopSignals" /> in the process.</param>
    internal McpStdioHost(
        McpServerInfo serverInfo,
        Func<McpConfinementRoot, McpResponseCap, IEnumerable<IMcpMethodHandler>> createHandlers,
        Stream input,
        Stream output,
        McpStandardErrorLog log,
        Func<string, string?> readEnvironmentVariable,
        TimeProvider timeProvider,
        Func<Action<PosixSignalContext>, IDisposable> registerStopSignals)
    {
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(createHandlers);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(readEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(registerStopSignals);
        this.serverInfo = serverInfo;
        this.createHandlers = createHandlers;
        this.input = input;
        this.output = output;
        this.log = log;
        this.readEnvironmentVariable = readEnvironmentVariable;
        this.timeProvider = timeProvider;
        this.registerStopSignals = registerStopSignals;
    }

    /// <summary>
    /// Registers <paramref name="handler" /> with the runtime for SIGINT (Ctrl+C), SIGQUIT (Ctrl+Break on
    /// Windows), SIGTERM and SIGHUP, as the generic host's console lifetime did.
    /// </summary>
    /// <param name="handler">Called on each of those signals.</param>
    /// <returns>What unregisters the handler from every signal.</returns>
    internal static IDisposable RegisterProcessStopSignals(Action<PosixSignalContext> handler) =>
        new StopSignalRegistrations([.. StopSignals.Select(signal => PosixSignalRegistration.Create(signal, handler))]);

    /// <summary>
    /// Reads the configuration, then serves until the input ends, <paramref name="cancellationToken" /> is
    /// cancelled or a stop signal arrives, and waits up to <see cref="ShutdownTimeout" /> for requests in flight.
    /// </summary>
    /// <param name="cancellationToken">Cancelling it stops the server as a stop signal does.</param>
    /// <returns>0 on a clean stop; 1 when reading the input or writing a response failed or requests in flight outlived <see cref="ShutdownTimeout" />.</returns>
    /// <exception cref="ArgumentException"><c>CURL_MCP_ROOT</c> is set but names no existing absolute folder.</exception>
    /// <exception cref="McpConfigurationException"><c>CURL_MCP_MAX_RESPONSE_BYTES</c> is not a positive whole number.</exception>
    internal async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        McpConfinementRoot root = McpConfinementRoot.FromEnvironment(readEnvironmentVariable);
        McpResponseCap cap = McpResponseCap.FromEnvironment(readEnvironmentVariable);
        if (root.ResolvedRoot is { } folder)
        {
            Directory.SetCurrentDirectory(folder);
        }

        using StdioMcpTransport transport = new(input, output);
        McpServer server = new(transport, serverInfo, createHandlers(root, cap), log.WriteLine);
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopRequested.Token);
        using IDisposable signals = registerStopSignals(StopOnSignal);
        log.WriteLine("Curl.Mcp is serving on standard input and output.");
        Task serving = server.RunAsync(stop.Token);
        await serving.WaitAsync(stop.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!serving.IsCompleted)
        {
            log.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Curl.Mcp is stopping; requests in flight have {ShutdownTimeout.TotalSeconds} seconds to finish."));
            await serving.WaitAsync(ShutdownTimeout, timeProvider).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        return ExitCodeFor(serving);
    }

    /// <summary>
    /// Handles a stop signal: keeps the runtime from ending the process at once, so requests in flight can
    /// finish, and stops the server.
    /// </summary>
    /// <param name="context">The signal's context; its <see cref="PosixSignalContext.Cancel" /> is set.</param>
    internal void StopOnSignal(PosixSignalContext context)
    {
        context.Cancel = true;
        stopRequested.Cancel();
    }

    /// <summary>Releases the source the stop signals cancel.</summary>
    public void Dispose() => stopRequested.Dispose();

    private int ExitCodeFor(Task serving)
    {
        if (!serving.IsCompleted)
        {
            log.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Curl.Mcp stopped with requests still in flight after {ShutdownTimeout.TotalSeconds} seconds."));
            return 1;
        }

        if (serving.Exception is { } failure)
        {
            string cause = failure.InnerException is McpResponseWriteException ? "writing a response" : "reading its input";
            log.WriteLine($"Curl.Mcp stopped because {cause} failed: {failure.InnerException}");
            return 1;
        }

        log.WriteLine("Curl.Mcp stopped.");
        return 0;
    }

    private sealed class StopSignalRegistrations(PosixSignalRegistration[] registrations) : IDisposable
    {
        public void Dispose()
        {
            foreach (PosixSignalRegistration registration in registrations)
            {
                registration.Dispose();
            }
        }
    }
}
