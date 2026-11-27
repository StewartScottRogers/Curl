namespace Curl.Networking;

/// <summary>
/// The connection number a connection being opened takes early, when a connection of its own,
/// a <c>--doh-url</c> one, is numbered before it has connected (ADR-1455).
/// </summary>
internal sealed class ConnectionNumberReservation
{
    /// <summary>Gets or sets the number taken early; <see langword="null" /> while none is.</summary>
    public long? Number { get; set; }
}
