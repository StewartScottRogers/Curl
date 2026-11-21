namespace Curl.Original.Protocol.Http;

/// <summary>
/// Hands the first QUIC attempt of a process a longer head start over TCP, once (BL-5660): the
/// hand-built QUIC and TLS 1.3 code is compiled when it first runs, which can take longer than
/// the happy-eyeballs timeout, so the first <c>--http3</c> attempt would lose to TCP on that
/// start-up cost alone. Every later attempt gets the plain timeout.
/// </summary>
public sealed class QuicColdStartGate
{
    private int _taken;

    /// <summary>Gets the gate every <see cref="HttpProtocolHandler" /> in the process shares.</summary>
    public static QuicColdStartGate Process { get; } = new();

    /// <summary>Takes the cold start: <see langword="true" /> for the first caller only.</summary>
    /// <returns>Whether this call was the first.</returns>
    public bool TryTakeColdStart() => Interlocked.Exchange(ref _taken, 1) == 0;
}
