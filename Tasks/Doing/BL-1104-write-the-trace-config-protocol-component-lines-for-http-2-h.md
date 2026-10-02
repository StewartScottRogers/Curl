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
completed:
---
# BL-1104 — Write the --trace-config protocol component lines for HTTP/2, HTTP/3, QUIC, SSH, FTP, mail and WebSocket

## Goal

Under `--trace-config <name>` (and `protocol`, `all`) Curl writes the component lines curl 8.21.0 writes for `http/2`, `http/3`, `quic`, `ssh`, `ftp`, `smtp`, `imap`, `pop3` and `ws`, each from that protocol's own code.

## Context

- ADR-0318 (BL-649) maps these components here; `CommandLineOptions.TraceComponents` holds the names. The reference Windows build has no HTTP/2 over TLS without ALPN agreement and no HTTP/3, so measure on the OpenSSL build where Windows cannot show them, as other protocol tasks have.
- This is a placeholder for several protocol libraries: split it with task-planner into one task per protocol library (touches that library, its tests and `Curl.Console`) before starting, and narrow `touches` from `*`.

## Acceptance criteria

- [ ] Split into one task per protocol, each with its measured lines in Notes, or done here protocol by protocol.
- [ ] Tests pin each component's lines and that none appears without its component.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
