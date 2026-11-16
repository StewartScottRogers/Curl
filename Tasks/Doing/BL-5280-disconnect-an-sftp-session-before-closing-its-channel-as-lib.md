---
id: BL-5280
title: Disconnect an SFTP session before closing its channel, as libssh2 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs, Curl.Original.Protocol.Ssh.UnitTests/SftpDisconnectOrderTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5280 — Disconnect an SFTP session before closing its channel, as libssh2 does

## Goal

When an SFTP transfer ends, the port sends `SSH_MSG_DISCONNECT` with the SFTP channel still open, as upstream curl 8.21.0 with libssh2 1.11.1 does, instead of closing the channel and waiting for its exit status first.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 2 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http.md`, finding 2.
- **Surl test:** `CurlPortBesideUpstreamCurlOverHttpTests.Download_RsaHostCertificateWithThePlainKeyPinned_DownloadsTheFile`, Surl commit `489f8d6f9b80614937f188b1a41896b8c5ed0e5c`.
- **Command line:** `curl -sS --hostpubsha256 <surl's ssh-rsa key SHA-256> -u tester:secret sftp://127.0.0.1:<port>/a.txt` against `surl -v --hostkey <RSA key> --hostcert <its certificate> sftp://127.0.0.1:0/`.
- **Upstream curl 8.21.0 (correct):** reference build, SHA-256 `0E773709C3A44DB47B88B71351D902027682ED87C3BD3821009E454BACCA8778`: exit 0, the file's 12 bytes; after `SFTP CLOSE /a.txt: read 12 bytes` the server notes `SSH disconnect received: 11 Shutdown`, then `SFTP session ended: client EOF`, and no channel end.
- **Port (defect):** exit 0, the same bytes; the server notes `SFTP session ended: client EOF`, `SSH channel 0 ended: exit status 0`, then `SSH disconnect received: 11 Shutdown` (same in three runs).
- **What decides:** RFC 4254 section 5.3 and RFC 4253 section 11.1 allow both orders; upstream curl 8.21.0's order is the expected one, since a server that watches the shutdown sees the two clients differently.
- **Where to start:** `Curl.Original.Protocol.Ssh.UnitLibrary/SshKeptSession.cs` (`ShutDownAsync`). Earlier related work: BL-3799, BL-4059.

## Acceptance criteria

- [ ] After an SFTP download the port sends `SSH_MSG_DISCONNECT` before any `SSH_MSG_CHANNEL_CLOSE` for the SFTP channel, shown by a port test of the message order; or a port ADR records why it keeps its order.

## Notes

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 3e370155.
