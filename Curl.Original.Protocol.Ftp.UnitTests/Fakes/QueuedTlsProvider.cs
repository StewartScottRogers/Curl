using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Ftp.Fakes;

/// <summary>
/// An <see cref="ITlsProvider" /> that answers each handshake with the next of
/// <paramref name="results" />, in order, standing a scripted connection in for the secured
/// one, and records every connection and host it was asked to secure. A failed result
/// disposes the plaintext connection, as the contract requires.
/// </summary>
/// <param name="results">The result of each handshake, in order.</param>
public sealed class QueuedTlsProvider(params ConnectResult[] results) : ITlsProvider
{
    /// <summary>Gets every plaintext connection and target host asked for, in order.</summary>
    public List<(IConnection Plaintext, string TargetHost)> Handshakes { get; } = [];

    /// <summary>Gets the events each handshake was asked to report to, in order.</summary>
    public List<ITransferEvents> HandshakeEvents { get; } = [];

    /// <summary>Gets or sets what runs as each handshake starts, to see what came before it.</summary>
    public Action BeforeEachHandshake { get; set; } = () => { };

    /// <summary>Gets the session source each session-resuming handshake named, in order.</summary>
    public List<IConnection> SessionSources { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether every handshake after the first (the control
    /// connection's) is refused unless it resumes the first one's session, naming the control
    /// connection, plaintext or secured, as its session source, as vsftpd's
    /// <c>require_ssl_reuse=YES</c> refuses a data connection that does not resume.
    /// </summary>
    public bool RequiresSessionReuse { get; set; }

    /// <summary>Gets the message of the exit 35 a refused, non-resuming handshake returns.</summary>
    public const string SessionReuseRequiredMessage = "522 SSL connection failed: session reuse required";

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientResumingSessionOfAsync(
        IConnection plaintext,
        IConnection sessionSource,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        SessionSources.Add(sessionSource);
        return ResumesControlSession(sessionSource)
            ? AnswerAsync(plaintext, targetHost, events, cancellationToken)
            : RefuseAsync(plaintext, targetHost);
    }

    /// <inheritdoc />
    public ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken) =>
        RequiresSessionReuse && Handshakes.Count > 0
            ? RefuseAsync(plaintext, targetHost)
            : AnswerAsync(plaintext, targetHost, events, cancellationToken);

    private bool ResumesControlSession(IConnection sessionSource) =>
        !RequiresSessionReuse
        || Handshakes.Count == 0
        || ReferenceEquals(sessionSource, Handshakes[0].Plaintext)
        || ReferenceEquals(sessionSource, results[0].Connection);

    private ValueTask<ConnectResult> AnswerAsync(
        IConnection plaintext,
        string targetHost,
        ITransferEvents events,
        CancellationToken cancellationToken)
    {
        HandshakeEvents.Add(events);
        return AuthenticateAsClientAsync(plaintext, targetHost, cancellationToken);
    }

    private async ValueTask<ConnectResult> RefuseAsync(IConnection plaintext, string targetHost)
    {
        Handshakes.Add((plaintext, targetHost));
        await plaintext.DisposeAsync();
        return ConnectResult.Failed(CurlExitCode.SslConnectError, SessionReuseRequiredMessage);
    }

    /// <inheritdoc />
    public async ValueTask<ConnectResult> AuthenticateAsClientAsync(
        IConnection plaintext,
        string targetHost,
        CancellationToken cancellationToken)
    {
        BeforeEachHandshake();
        Handshakes.Add((plaintext, targetHost));
        ConnectResult result = results[Handshakes.Count - 1];
        if (result.Connection is null)
        {
            await plaintext.DisposeAsync();
        }

        return result;
    }
}
