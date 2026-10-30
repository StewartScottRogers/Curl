# Curl.Extension.QuicMigration.UnitLibrary

The `--quic-migrate` extension (ADR-0999, ADR-1149): a QUIC transfer (HTTP/3) moves to a new local address when its
network path is lost after the handshake, as RFC 9000 section 9 connection migration. Namespace
`Curl.Extension.QuicMigration`. Tested against RFC 9000 section 9 and draft-ietf-quic-multipath, not against curl.

- `QuicMigrationExtension` declares the `quic-migrate` flag, the extension's `ExtensionDescription`, and `Activate`,
  the connection hook activator `Curl.Extension.Composition` registers: `null` without `--quic-migrate`.
- `QuicMigrationConnectionHook` keeps curl's stream connector and wraps curl's datagram connector in a
  `MigratingDatagramConnector`, whose channels are `MigratingDatagramChannel`s.
- `MigratingDatagramChannel` passes every datagram through unchanged. Once a 1-RTT packet (`QuicPacketForm`) went each
  way, a send or receive failing with a `LocalPathLoss` socket error opens a fresh channel to the same server through
  curl's connector, disposes the old one and carries on there. One path at a time; no new connection ID, no
  `PATH_CHALLENGE` and no multipath, which need the QUIC stack (ADR-1149).

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.QuicMigration.UnitTests`.
