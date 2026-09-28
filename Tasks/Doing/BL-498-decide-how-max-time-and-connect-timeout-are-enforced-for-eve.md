---
id: BL-498
title: Decide how --max-time and --connect-timeout are enforced for every scheme
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-498 — Decide how --max-time and --connect-timeout are enforced for every scheme

## Goal

An ADR fixes one way every protocol handler, present and future, ends a transfer with exit 28 when `-m`/`--max-time` or `--connect-timeout` runs out, and what the connector owns versus the handler or the runner.

## Context

- Conformance audit 2026-09-28, row 12 (Blocker): only the HTTP and TFTP handlers read `ITransferContext.MaxTime` and `ConnectTimeout`; FTP, DICT, Gopher, Telnet and MQTT never time out, and `Curl.Networking.UnitLibrary/TcpConnector.cs` ignores `--connect-timeout`.
- Precedents: ADR-0008 (the context carries connect timeout and max time), ADR-0040 (HTTP enforces them in the handler), `Curl.Protocol.Tftp.UnitLibrary/TftpTimeLimits.cs`, and ADR-0106 (the runner watches each attempt for low speed and cancels it, a runner-level precedent).
- curl's messages differ by phase: `Connection timed out after <ms> milliseconds` / `Failed to connect to <host> port <p> after <ms> ms: Timed out` during connect, and `Operation timed out after <ms> milliseconds with <n> bytes received` (or `out of <m>`) during the transfer. The message needs the byte counts the handler reports through `ITransferProgress`.
- New handlers (SMTP, POP3, IMAP, SSH, WebSocket, LDAP, RTSP, SMB) are planned to depend on this ADR, so it must say what a new handler has to do (ideally nothing beyond honouring `CancellationToken` and reporting progress).
- Rules: inject `TimeProvider`, never `Thread.Sleep`; async all the way.

## Acceptance criteria

- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", with alternatives weighed (per-handler as HTTP does; a shared runner deadline; a connector-level connect deadline).
- [ ] The Decision states where the connect deadline is enforced (and so what `ConnectTarget` or the connector needs), where the whole-transfer deadline is enforced, how the byte counts reach the exit-28 message, and whether HTTP and TFTP keep their own enforcement.
- [ ] The Consequences list the changes BL-510 (TcpConnector), BL-511 (dict, gopher, telnet, mqtt) and BL-512 (ftp, ftps) make, and the contract a new protocol handler follows.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
