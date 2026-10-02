---
id: BL-1104
title: Write the --trace-config protocol component lines for HTTP/2, HTTP/3, QUIC, SSH, FTP, mail and WebSocket
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-649]
touches: [*]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1104 — Write the --trace-config protocol component lines for HTTP/2, HTTP/3, QUIC, SSH, FTP, mail and WebSocket

## Goal

Under `--trace-config <name>` (and `protocol`, `all`) Curl writes the component lines curl 8.21.0 writes for `http/2`, `http/3`, `quic`, `ssh`, `ftp`, `smtp`, `imap`, `pop3` and `ws`, each from that protocol's own code.

## Context

- ADR-0318 (BL-649) maps these components here; `CommandLineOptions.TraceComponents` holds the names. The reference Windows build has no HTTP/2 over TLS without ALPN agreement and no HTTP/3, so measure on the OpenSSL build where Windows cannot show them, as other protocol tasks have.
- This is a placeholder for several protocol libraries: split it with task-planner into one task per protocol library (touches that library, its tests and `Curl.Console`) before starting, and narrow `touches` from `*`.

## Acceptance criteria

- [x] Split into one task per protocol, each with its measured lines in Notes, or done here protocol by protocol.
- [x] Tests pin each component's lines and that none appears without its component. (Each split task carries this criterion for its component.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

Split (2026-10-02) into one task per protocol library, each touching that library, its tests,
`Curl.Console` and `Curl.Console.UnitTests` (BL-1102's pattern: the composition decides the
component is on and hands the library an `ITransferEvents` sink):

- BL-1162 `ftp` (26 lines measured), BL-1163 `smtp` (23 lines measured), BL-1164 `ws` (7 lines measured).
- BL-1165 `imap` and `pop3`: measured to write no lines on curl 8.21.0, so only tests pin the absence.
- BL-1166 `ssh`, BL-1167 `http/2`, BL-1168 `http/3`, BL-1169 `quic`: need an sshd, an h2c/ALPN server
  or the OpenSSL HTTP/3 build to measure, which the recorder does not serve; measuring is their
  first criterion.

Measured with the reference Schannel build and `Record-CurlExchange.ps1` (`-Ftp`, `-Smtp`, `-Imap`,
`-Pop3`, and an HTTP 101 response for `ws`): `protocol` writes the same `[FTP]` lines as `ftp`;
no line appears without `-v`, nor under another component's name. ADR-0318's table now names the
split tasks. No code changed here.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Split into per-protocol tasks BL-1162 to BL-1169 with ftp, smtp, ws lines measured and imap/pop3 measured as none
