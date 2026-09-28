---
id: BL-464
title: Retry an FTP active-mode listen on the control connection's address when the -P address is not local
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-437, BL-456]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-464 — Retry an FTP active-mode listen on the control connection's address when the -P address is not local

## Goal

When `-P/--ftp-port` names an address that is not local to this machine, active mode binds its listening socket on the control connection's local address instead (once), as curl 8.21.0 does, while `EPRT`/`PORT` still announce the `-P` address.

## Context

- Upstream behaviour, curl 8.21.0 `lib/ftp.c`, `ftp_port_bind_socket`: when the `-P` address was given as a host or address (curl's `non_local` flag) and `bind()` fails with `EADDRNOTAVAIL`, curl emits the info line `bind(port=%hu) on non-local address failed: %s` (visible with `-v`), replaces the bind address with the control connection's local address (`getsockname` on the control socket) and restarts the port loop once. The address sent in `EPRT`/`PORT` stays the user's `-P` address. Any other bind error, or a second failure, is still `failf(data, "bind(port=%hu) failed: %s")` and exit 30 (`CURLE_FTP_PORT_FAILED`, https://curl.se/libcurl/c/libcurl-errors.html). Man page for `-P`: https://curl.se/docs/manpage.html.
- Today: BL-456 added `TcpConnectionListener` (`Curl.Networking.UnitLibrary/TcpConnectionListener.cs`), which fails at once with exit 30 and `bind(port=N) failed: <reason>` because `ListenTarget` (`Curl.Protocol.Abstractions.UnitLibrary/ListenTarget.cs`) holds one address. BL-437 adds active mode to `FtpProtocolHandler` (`Curl.Protocol.Ftp.UnitLibrary`) on top of `IConnectionListener` per ADR-0102 (`Documentation/Planning/Decisions/ADR-0102-ftp-active-mode-and-tls-need-a-listening-seam-and-four-transfer-options.md`). `IConnection.LocalEndPoint` (`Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs`) is the control connection's local address.
- Decide where the retry belongs and record it in an ADR marked "Decided by Claude under Stewart's delegation" (root `CLAUDE.md`, "Decisions"). The two candidates: (a) `FtpProtocolHandler` retries `ListenAsync` once with the control connection's `IConnection.LocalEndPoint` address when `ListenResult` reports a distinguishable "address not available" failure (so `TcpConnectionListener` must report that case distinctly, within this task's touches); (b) a fallback address on `ListenTarget` in `Curl.Protocol.Abstractions.UnitLibrary`. Prefer (a): it stays inside this task's touches and keeps the contract unchanged unless `ListenResult` has no way to carry the distinction. If the ADR chooses (b), or (a) needs a change to `Curl.Protocol.Abstractions.UnitLibrary`, that project is outside this task's touches: have `task-planner` file the contract task, add it to `depends-on`, and move this task to `Blocked` on it (task-board skill, "Claiming and finishing", step 4).
- Measure before pinning: run `Record-CurlExchange.ps1 -Ftp -CurlArgs -v,-P,192.0.2.1 ...` (192.0.2.1 is TEST-NET-1, never local) against curl 8.21.0 and pin the recorded `-v` stderr line, the `EPRT` (or `PORT`) command bytes and exit code. `Record-CurlExchange.ps1` is not in touches; if it cannot record this case, file a task for the recorder rather than editing it. The `%s` reason text differs by platform (Schannel build on Windows, OpenSSL build on Linux and macOS, `strerror` vs Winsock text): pin each platform's text in its own `[OSCondition]` test, using `ConnectFailureReason.Describe` as `TcpConnectionListenerTests` does.

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's delegation", states where the non-local-address retry lives and why, and cites curl 8.21.0 `ftp_port_bind_socket`.
- [x] `Curl.Networking.UnitTests` has a named test showing `TcpConnectionListener` reports a bind to an address that is not local (for example 192.0.2.1) as the distinguishable "address not available" failure the ADR names, and a named test that any other bind failure (EACCES/in-use) still returns `CurlExitCode` 30 with `bind(port=N) failed: <reason>`.
- [x] `Curl.Protocol.Ftp.UnitTests` has a named test, using a fake `IConnectionListener` and a fake control `IConnection` with a `LocalEndPoint`, showing that when the first listen on the `-P` address fails as "address not available", the handler listens again once on the control connection's local address, writes the `-v` info line `bind(port=N) on non-local address failed: <reason>` as measured with curl 8.21.0, and sends `EPRT |1|192.0.2.1|<port>|` (the `-P` address, not the control connection's) byte-for-byte as recorded.
- [x] A named test in `Curl.Protocol.Ftp.UnitTests` shows that when the retry on the control connection's address also fails, the transfer ends with `CurlExitCode` 30 (`CURLE_FTP_PORT_FAILED`) and curl 8.21.0's measured `bind(port=N) failed: <reason>` message, with no third attempt; and one shows that `-P -` (no user address) does not retry.
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` and `dotnet build Curl.Networking.UnitLibrary -warnaserror` are clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration` or the network beyond loopback.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, cyclomatic complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary` and `Curl.Networking.UnitLibrary`.

## Notes

- Measured 2026-09-27, curl 8.21.0 Schannel: `Record-CurlExchange.ps1 -Port 47464 -Ftp -CurlArgs -v,-P,192.0.2.1,ftp://127.0.0.1:47464/f.txt -FtpReply 'EPRT=500 no','PORT=500 no'`
  sent `EPRT |1|192.0.2.1|61200|` then `PORT 192,0,2,1,239,17`, printed
  `* bind(port=0) on non-local address failed: Address not available` before each, and exited 30
  `Failed to do PORT`. Each bind retries on its own; both are pinned.
- Decision (ADR-0107): option (a). `TcpConnectionListener` words `EADDRNOTAVAIL` as curl's `-v`
  line `bind(port=N) on non-local address failed: <reason>`; `FtpSession` recognises the phrase
  (`FtpTransferMessages.NonLocalBindFailed`), reports it through `ITransferEvents.ReportInfo`,
  listens once more on the control connection's address, and announces the `-P` address with the
  bound port. No contract change in `Curl.Protocol.Abstractions.UnitLibrary`.
- Default taken: with no retry (`-P -`, unknown control address) or a failed retry, the exit 30
  message is the listener's reworded to `bind(port=N) failed: <reason>`, curl's `failf` text. The
  second-failure text comes from curl's source (not provokable on loopback); the phrase shape
  matches the measured BL-456 `bind(port=N) failed:` line.
- `TcpConnectionListener.MovesOnToTheNextPort` folded into `BindFailureMessage`, which now owns
  the three outcomes (next port, non-local, failed).
- The console's `TransferContextFactory` sets `Events` for every scheme, so under `-v` the line
  reaches stderr through the same path as the HTTP info lines.
- `Measure-CodeQuality.ps1`: Ftp and Networking 100/100, worst CRAP 10. Its two failing
  `Curl.Console` members are already filed (BL-432, BL-455, BL-462).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. A non-local -P address is bound again once on the control connection's address with curl's -v line, still announced in EPRT/PORT
