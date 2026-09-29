---
id: BL-903
title: Fail FTP over IPv6 with exit 8 when EPSV is refused instead of falling back to PASV
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-903 — Fail FTP over IPv6 with exit 8 when EPSV is refused instead of falling back to PASV

## Goal

When the control connection is IPv6 and `EPSV` is answered with anything but `229`, the FTP handler ends with exit 8 (`CURLE_WEIRD_SERVER_REPLY`) and `Failed EPSV attempt, exiting`, without sending `PASV`, as curl 8.21.0 does.

## Context

- Found in BL-662. Measured with `Record-CurlExchange.ps1 -Ftp -ListenAddress ::1 -FtpReply 'EPSV=500 no'` and `curl -sS -g ftp://[::1]:<port>/f.txt`: stderr `curl: (8) Failed EPSV attempt, exiting`, exit 8, last commands `PWD`, `EPSV`. With `--disable-epsv` curl sends `PASV` on IPv6 and downloads (exit 0).
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `OpenPassiveDataConnectionAsync`, which today falls back to `PASV`. Whether the control connection is IPv6 must come from the connection (a host name can resolve to `::1`), not only from a bracketed URL host; if `IConnection` cannot tell, that needs an Abstractions task first.
- Measure whether curl sends `QUIT` first, and what `-v` prints (`Failed EPSV attempt. Disabling EPSV` is the IPv4 line).

## Acceptance criteria

- [x] Measured first: the case above with `-v`, with a host name resolving to `::1`, and with `--disable-epsv`; commands, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the exit 8 case and that IPv4 still falls back to `PASV`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 with curl 8.21.0 (Schannel, mingw64) via `Record-CurlExchange.ps1 -Ftp -ListenAddress ::1 -FtpReply 'EPSV=500 no'`:

- `curl -v -sS -g ftp://[::1]:18903/f.txt`: exit 8; commands `USER`, `PASS`, `PWD`, `EPSV` and nothing more (no `PASV`, no `QUIT`). `-v` after `> EPSV`: `* Connect data stream passively`, `< 500 no`, `* Failed EPSV attempt, exiting`, `* shutting down connection #0`, then `curl: (8) Failed EPSV attempt, exiting`.
- `curl -v -sS -6 ftp://localhost:18903/f.txt` (localhost resolves to `::1`): identical, so the decision comes from the connected peer, not the URL's brackets.
- `curl -sS -g --disable-epsv ftp://[::1]:18903/f.txt`: **still sends `EPSV`** (curl re-enables EPSV on IPv6), exit 8, same stderr. The Context's claim that it sends `PASV` was wrong; the handler now ignores `--disable-epsv` over IPv6 too.

Implementation: `FtpSession.controlPeerIsIPv6` reads `IConnection.RemoteEndPoint` (an IPv4-mapped peer counts as IPv4), so no Abstractions change was needed. `AnswerRefusedEpsvAsync` returns exit 8 with no `QUIT` over IPv6, else the existing `PASV` fallback. The `* shutting down connection` line follows the existing plain-failure pattern (not reported by the handler for other non-`QUIT` failures either). No ADR: behaviour copied from measurement, no choice made.

Tests: `ExecuteAsync_EpsvRefusedOverIPv6_FailsWithExit8AndSendsNeitherPasvNorQuit`, `ExecuteAsync_DisableEpsvOverIPv6_StillSendsEpsv`, `ExecuteAsync_EpsvRefusedOverIPv4MappedPeer_StillFallsBackToPasv`; the existing `ExecuteAsync_EpsvRefused_ReportsCurlsFallbackLineAndNoSecondPassiveLine` pins IPv4's `PASV` fallback. Ftp tests 450 passed; Measure-CodeQuality: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP over IPv6 ends with exit 8 'Failed EPSV attempt, exiting' when EPSV is refused, and sends EPSV despite --disable-epsv, as curl 8.21.0 does
