namespace Curl.Quic;

/// <summary>Shared set-up for the handshake tests.</summary>
internal static class QuicHandshakeTest
{
    /// <summary>The settings of curl's build for <c>localhost</c>.</summary>
    public static QuicClientSettings CurlSettings { get; } = new() { Tls = QuicClientSettings.CreateLibreSslTlsSettings("localhost") };

    /// <summary>A client with curl's settings, fixed randomness, a verifier that accepts every chain and a clock that moves only when <paramref name="clock" /> is advanced.</summary>
    public static QuicClientHandshake Client(QuicClientSettings? settings = null, QuicTestVerifier? verifier = null, ManualTimerTimeProvider? clock = null) =>
        new(settings ?? CurlSettings, new QuicTestRandomSource(), verifier ?? new QuicTestVerifier(), clock ?? new ManualTimerTimeProvider());
}
