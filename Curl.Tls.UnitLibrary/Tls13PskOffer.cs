namespace Curl.Tls;

/// <summary>What a resuming ClientHello offers besides its key shares: the ticket's identity, room for its binder, and whether it asks for 0-RTT.</summary>
/// <param name="Identity">The ticket and its obfuscated age.</param>
/// <param name="BinderLength">The length of the binder, the session hash's length; the builder leaves zeros for the handshake to replace.</param>
/// <param name="EarlyData">Whether the hello carries <c>early_data</c>.</param>
internal sealed record Tls13PskOffer(PskIdentity Identity, int BinderLength, bool EarlyData);
