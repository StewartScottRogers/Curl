---
id: BL-5918
title: Match OpenSSL curl's exit code when a TELNET connection is reset on Linux and macOS
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-5882]
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs, Curl.Original.Protocol.Telnet.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-24
completed:
---
# BL-5918 — Match OpenSSL curl's exit code when a TELNET connection is reset on Linux and macOS

## Goal

On Linux and macOS a `telnet://` session whose connection is reset ends with the exit code and message OpenSSL curl gives.

## Context

BL-5882 and ADR-1352 pin the Windows behaviour: a reset exits 0 unless it strikes the option negotiation (then 56). The handler applies that on every platform. curl's non-Windows telnet loop (`lib/telnet.c`, `telnet_do`) polls the socket and calls `recv`, so a reset there is expected to fail the `recv` and exit 56 `Recv failure: Connection reset by peer`. Measure it (curl 8.18.0 or later on Linux) before changing `TelnetProtocolHandler.FailedReadResult`.

## Acceptance criteria

- [ ] The exit code and stderr of real curl on Linux for a reset of an idle TELNET session, with and without negotiation, are recorded under Notes.
- [ ] Unit tests marked with `OSCondition` pin each platform's answer in `Curl.Original.Protocol.Telnet.UnitTests`, and the Windows answers of ADR-1352 are unchanged.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
