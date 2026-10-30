# Curl.Extension.WireGuard.UnitLibrary

The WireGuard extension (ADR-0999), tested against wg(8) and wg-quick(8), not against curl. Namespace
`Curl.Extension.WireGuard`. It references `Curl.Cryptography.UnitLibrary`; it may also reference
`Curl.Protocol.Abstractions.UnitLibrary` and `Curl.Extension.Abstractions.UnitLibrary`, never a `Curl.Protocol.*`
library or another extension. It is not yet registered in any Composition catalogue and has no `--wireguard` option
(BL-4147 wires both).

- `Configuration/` reads a wg-quick configuration file (ADR-1117): `WireGuardConfigurationParser.Parse` gives a
  `WireGuardConfiguration` holding the `WireGuardInterface` (`PrivateKey`, `Address` as `WireGuardCidr`s, `DNS`,
  `ListenPort`, `MTU`) and its `WireGuardPeer`s (`PublicKey`, `PresharedKey`, `Endpoint` as a `WireGuardEndpoint`,
  `AllowedIPs`, `PersistentKeepalive`). Keys are 32 bytes of base64; wg-quick's host-side keys (`Table`, `FwMark`,
  `SaveConfig` and the up and down hooks) are ignored. Every error throws `WireGuardConfigurationException`, a
  `FormatException` whose message starts `line N:`.
- `IpStack/` is a user-space IPv4, IPv6 and TCP client (ADR-1121) over an `IIpPacketChannel` that sends and receives
  raw IP packets. `IpPacketCodec` and `TcpSegmentCodec` build and parse packets and segments with their RFC 1071
  `InternetChecksum`s; `TcpConnection.ConnectAsync` opens one connection (handshake with MSS, in-order receive,
  window-limited send, `RetransmissionTimeoutEstimator`'s RFC 6298 timer on an injected `TimeProvider`, FIN and RST);
  `TcpConnectionStream` exposes it as an asynchronous-only `Stream`; `UserSpaceTcpClient.ConnectAsync` picks a random
  ephemeral port and initial sequence number.
- `Messages/` lays out the four messages of whitepaper 5.4.2 to 5.4.7: `HandshakeInitiationMessage` (type 1, 148
  bytes), `HandshakeResponseMessage` (type 2, 92), `CookieReplyMessage` (type 3, 64) and `TransportDataMessage`
  (type 4, 32 or more), each with `Encode` and `TryDecode` (`false` for any other datagram); `WireGuardMessageType`;
  `WireGuardMessageAuthentication` derives the mac1 and cookie keys and writes and checks mac1 and mac2; the internal
  `WireGuardMessageLayout` holds the shared header and field writes.
- `Noise/` runs the initiator's Noise_IKpsk2_25519_ChaChaPoly_BLAKE2s handshake:
  `WireGuardHandshakeInitiator` (`CreateInitiation`, `TryConsumeCookieReply`, `TryConsumeResponse`, `ConnectAsync`
  over an `IWireGuardDatagramChannel`, `ForPeer` from the configuration), with the clock and a
  `RandomNumberGenerator` injected; `WireGuardTransportSession` (`Seal` pads to 16 bytes under a 64-bit counter,
  `TryOpen`, `NeedsRekey`, section 6.1's reject limits); internal `NoiseSymmetricState` (C, H, KDF1 to KDF3, AEAD),
  `WireGuardReplayWindow` (RFC 6479, 8128 counters) and `WireGuardTai64nTimestamp`. Its tests drive the initiator
  against `WireGuardTestResponder`, written from the whitepaper on `Curl.Cryptography` alone.

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.WireGuard.UnitTests`.
