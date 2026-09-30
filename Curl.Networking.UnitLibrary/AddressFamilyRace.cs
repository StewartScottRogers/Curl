using System.Net;
using System.Net.Sockets;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Dials a host's addresses as curl 8.21.0's happy eyeballs does: the addresses of the first
/// address's family in turn, and the other family's in turn beside them, started once
/// <c>--happy-eyeballs-timeout-ms</c> has passed on the injected <see cref="TimeProvider" /> or as
/// soon as every address of the first family has failed, whichever comes first. The first
/// connection made wins; the attempts still running are cancelled, and any that connects anyway is
/// closed, all without a line on the events, as curl prints nothing for them (measured, BL-644 Notes).
/// </summary>
/// <remarks>
/// Every attempt is started, and every outcome reported, from the one loop in <see cref="DialAsync" />,
/// so the events and the diagnostic log see one line at a time in the order curl prints them:
/// <c>Trying</c> before each attempt, and the <c>connect to ... failed</c> line when one fails (BL-408).
/// </remarks>
/// <param name="tcpDialer">Opens a plaintext connection to one address.</param>
/// <param name="timeProvider">Runs the happy-eyeballs delay.</param>
/// <param name="happyEyeballsTimeout">How long the first family runs alone.</param>
/// <param name="events">Where the <c>Trying</c> and <c>connect to ... failed</c> lines go.</param>
/// <param name="log">Where each dial is logged.</param>
internal sealed class AddressFamilyRace(
    ITcpDialer tcpDialer,
    TimeProvider timeProvider,
    TimeSpan happyEyeballsTimeout,
    ITransferEvents events,
    NetworkDiagnosticLog log)
{
    private readonly List<Attempt> _running = [];
    private SocketError _lastError = SocketError.Success;
    private LocalBindFailure? _lastBindFailure;

    /// <summary>
    /// Races the families of <paramref name="addresses" /> on <paramref name="port" />.
    /// </summary>
    /// <param name="addresses">The addresses to dial, at least one, in the resolver's order.</param>
    /// <param name="port">The port to dial on each.</param>
    /// <param name="cancellationToken">Cancels every attempt.</param>
    /// <returns>
    /// The winning connection and the end point it was dialled at, or no connection, the
    /// <see cref="SocketError" /> of the attempt that failed last, which curl keeps as
    /// <c>CURLINFO_OS_ERRNO</c>, and that attempt's <see cref="LocalBindFailure" /> when its local end
    /// could not be bound.
    /// </returns>
    public async ValueTask<(DialedTcpConnection? Dialed, IPEndPoint? RemoteEndPoint, SocketError LastError, LocalBindFailure? LastBindFailure)> DialAsync(
        IReadOnlyList<IPAddress> addresses,
        int port,
        CancellationToken cancellationToken)
    {
        var firstFamily = addresses[0].AddressFamily;
        var first = new Queue<IPEndPoint>(addresses.Where(address => address.AddressFamily == firstFamily).Select(address => new IPEndPoint(address, port)));
        var second = new Queue<IPEndPoint>(addresses.Where(address => address.AddressFamily != firstFamily).Select(address => new IPEndPoint(address, port)));
        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            StartNext(first, race.Token);
            Task? secondFamilyDue = second.Count == 0 ? null : Task.Delay(happyEyeballsTimeout, timeProvider, race.Token);
            while (_running.Count > 0)
            {
                var completed = await Task.WhenAny(RunningTasks(secondFamilyDue)).ConfigureAwait(false);
                if (completed == secondFamilyDue)
                {
                    // A delay cancelled by the caller is no reason to start the other family.
                    race.Token.ThrowIfCancellationRequested();
                    secondFamilyDue = null;
                    StartNext(second, race.Token);
                    continue;
                }

                var attempt = _running.Single(running => running.Dial == completed);
                _running.Remove(attempt);
                if (await OutcomeOfAsync(attempt).ConfigureAwait(false) is { } dialed)
                {
                    return (dialed, attempt.RemoteEndPoint, SocketError.Success, null);
                }

                StartNext(attempt.Family, race.Token);
                if (_running.Count == 0 && secondFamilyDue is not null)
                {
                    // The first family has failed on every address: curl starts the other at once.
                    secondFamilyDue = null;
                    StartNext(second, race.Token);
                }
            }

            return (null, null, _lastError, _lastBindFailure);
        }
        finally
        {
            await race.CancelAsync().ConfigureAwait(false);
            await CloseAbandonedAsync().ConfigureAwait(false);
        }
    }

    private IEnumerable<Task> RunningTasks(Task? secondFamilyDue)
    {
        foreach (var attempt in _running)
        {
            yield return attempt.Dial;
        }

        if (secondFamilyDue is not null)
        {
            yield return secondFamilyDue;
        }
    }

    /// <summary>
    /// Starts the next address of <paramref name="family" />, reporting its <c>Trying</c> line,
    /// unless every address of it has been tried.
    /// </summary>
    private void StartNext(Queue<IPEndPoint> family, CancellationToken cancellationToken)
    {
        if (family.TryDequeue(out var remoteEndPoint))
        {
            events.ReportInfo($"  Trying {remoteEndPoint}...");
            log.Dialling(remoteEndPoint);
            _running.Add(new Attempt(remoteEndPoint, family, DialOneAsync(remoteEndPoint, cancellationToken)));
        }
    }

    // An async method, so a dialer that throws before it returns fails the task instead.
    private async Task<DialedTcpConnection> DialOneAsync(IPEndPoint remoteEndPoint, CancellationToken cancellationToken) =>
        await tcpDialer.DialAsync(remoteEndPoint, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Gives the connection a finished attempt made, or reports its failure and gives
    /// <see langword="null" />. Anything but a <see cref="SocketException" />, a cancellation
    /// included, escapes.
    /// </summary>
    private async ValueTask<DialedTcpConnection?> OutcomeOfAsync(Attempt attempt)
    {
        try
        {
            var dialed = await attempt.Dial.ConfigureAwait(false);
            log.Connected(attempt.RemoteEndPoint, dialed.LocalEndPoint);
            return dialed;
        }
        catch (SocketException exception)
        {
            // curl moves on to the next address; only when every one fails is it exit 7.
            _lastError = exception.SocketErrorCode;
            _lastBindFailure = (exception as LocalBindException)?.Failure;
            events.ReportInfo(ConnectFailedLine(attempt.RemoteEndPoint, exception));
            log.DialFailed(attempt.RemoteEndPoint, exception);
            return null;
        }
    }

    /// <summary>
    /// Waits for each attempt still running once the race is over, now cancelled, and closes any
    /// that connected anyway.
    /// </summary>
    private async ValueTask CloseAbandonedAsync()
    {
        foreach (var attempt in _running)
        {
            await ((Task)attempt.Dial).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (attempt.Dial.IsCompletedSuccessfully)
            {
                var dialed = await attempt.Dial.ConfigureAwait(false);
                await dialed.Connection.DisposeAsync().ConfigureAwait(false);
            }
        }

        _running.Clear();
    }

    /// <summary>
    /// curl 8.21.0's line for one failed dial, such as <c>connect to 127.0.0.1 port 1 from
    /// 0.0.0.0 port 56585 failed: Connection refused</c>. A failed dial reports no local end
    /// point, so it names the unspecified address of the family and port <c>0</c> (ADR-0100). A dial
    /// whose local end could not be bound names no local address at all and errno 0's reason, as in
    /// <c>connect to 127.0.0.1 port 47599 from  port 0 failed: No error</c> (measured, BL-600 Notes).
    /// </summary>
    private static string ConnectFailedLine(IPEndPoint remoteEndPoint, SocketException exception)
    {
        if (exception is LocalBindException)
        {
            return $"connect to {remoteEndPoint.Address} port {remoteEndPoint.Port} from  port 0 failed: {LocalBindException.ReasonText(OperatingSystem.IsWindows())}";
        }

        var unspecified = remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
        var reason = ConnectFailureReason.Describe(exception, OperatingSystem.IsWindows());
        return $"connect to {remoteEndPoint.Address} port {remoteEndPoint.Port} from {unspecified} port 0 failed: {reason}";
    }

    /// <summary>One dial in flight: where it goes, the family it belongs to, and its outcome.</summary>
    private sealed record Attempt(IPEndPoint RemoteEndPoint, Queue<IPEndPoint> Family, Task<DialedTcpConnection> Dial);
}
