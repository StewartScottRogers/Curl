---
id: BL-398
title: Measure tftp:// through SOCKS and HTTPS proxies and match curl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-345]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-398 — Measure tftp:// through SOCKS and HTTPS proxies and match curl

## Goal

`tftp://` with a SOCKS (`--socks5`, `--socks4`, …) or HTTPS proxy behaves as curl 8.21.0's Schannel build does, never sending TFTP datagrams around a proxy curl would use.

## Context

- ADR-0056 rule 4 covers only an HTTP proxy; BL-345 implemented that. For every other `ProxyKind`, `TftpProtocolHandler` still opens the datagram channel straight to the server.
- ADR-0056 rule 6 (the ADR-0053 guard in `Curl.Console`) may already refuse some of these with exit 4 before the handler runs; check what reaches the handler.
- Measure with `Record-CurlExchange.ps1` (for SOCKS, a loopback listener recording the greeting) before pinning anything.

## Acceptance criteria

- [ ] The measured commands, bytes, stderr and exit codes for `--socks5`, `--socks4` and an `https://` proxy with `tftp://example.com/f` are recorded under this task's Notes.
- [ ] A test in `Curl.Protocol.Tftp.UnitTests` pins each measured outcome the handler is responsible for.
- [ ] 100% line and branch coverage of the changed code; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-09-27: Created.
