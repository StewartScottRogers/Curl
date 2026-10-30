# ADR-1149 — --quic-migrate moves a QUIC transfer to a new local address after path loss

- **Status:** Accepted
- **Date:** 2026-10-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4146.

## Context

BL-4146 asks for an extension (ADR-0999) that keeps a QUIC transfer alive across a local address change, following
RFC 9000 section 9 (connection migration) and draft-ietf-quic-multipath. Upstream curl has no such option, so there
is no curl behaviour to match; without the option every transfer must stay exactly as it is. Since ADR-1118, HTTP/3's
QUIC dialer opens its channels through an activated connection hook's datagram connector (ADR-1010), which is the only
seam an extension has into a QUIC connection: it sees the UDP datagrams, not the QUIC stack's connection IDs, frames or
keys.

## Decision

1. The extension is `Curl.Extension.QuicMigration.UnitLibrary`, one flag, `--quic-migrate`, registered like
   `--npipe` (ADR-1021) in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and
   `ExtensionConnectionHookCatalogue`. Its connection hook keeps curl's stream connector and wraps curl's datagram
   connector in `MigratingDatagramConnector`.
2. Specifications followed: RFC 9000 (May 2021) sections 9, 9.2, 9.3 and 17.3.1, and draft-ietf-quic-multipath as
   current on 2026-10-30, section 2 only (an endpoint that has not negotiated multipath uses one path at a time).
3. Each channel is a `MigratingDatagramChannel` that passes every datagram through byte for byte. The handshake counts
   as confirmed once a 1-RTT (short header, section 17.3.1) packet went each way; before that no migration happens
   (section 9: no migration before the handshake is confirmed).
4. After that, a send or receive that fails with a local path loss socket error (`AddressNotAvailable`,
   `NetworkDown`, `NetworkUnreachable`, `NetworkReset`, `HostUnreachable`) opens a fresh channel to the same host and
   port through curl's connector, so the operating system picks the new local address, disposes the old channel and
   carries on there (section 9.2, client-initiated migration). The server validates the new path itself (section
   9.3). Any other failure, or a new channel that does not open, ends the transfer with the original failure, as
   without the option.
5. One path at a time (multipath section 2): the old channel is disposed before the new one carries a datagram, and a
   receive still pending on the old channel carries on over the new one.

## Left out, and why

- **A fresh connection ID on the new path (RFC 9000 section 9.5)**, **PATH_CHALLENGE / PATH_RESPONSE probing before
  moving (section 9.1)** and **multipath proper (draft-ietf-quic-multipath's transport parameter, PATH_ACK and
  per-path packet number spaces)**: each needs the QUIC stack's connection IDs, frames and keys, which an extension
  cannot reach through a datagram connector. They need a seam into `Curl.Protocol.Quic`'s connection, which is work
  for its own task, not a reason to keep the feature out. Without section 9.5, the new path reuses the current
  connection ID, which servers accept but which lets an on-path observer link the two paths.
- **Server-initiated migration and preferred addresses (section 9.6)**: the client follows what curl's QUIC stack
  already does.

## Consequences

- Without `--quic-migrate`, and with `CURL_EXTENSIONS=0` (which refuses the option as unknown, exit 2), nothing
  changes.
- Tested against RFC 9000 section 9 and multipath section 2 with fake datagram channels, no network.
