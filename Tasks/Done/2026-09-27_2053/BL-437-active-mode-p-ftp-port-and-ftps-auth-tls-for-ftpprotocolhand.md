---
id: BL-437
title: Active mode (-P/--ftp-port) and ftps:// / AUTH TLS for FtpProtocolHandler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-431, BL-459]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-437 — Active mode (-P/--ftp-port) and ftps:// / AUTH TLS for FtpProtocolHandler

## Goal

`FtpProtocolHandler` supports active mode (`-P/--ftp-port`: `EPRT`/`PORT` and a data connection the server opens back) and TLS (`ftps://` implicit TLS, and explicit `AUTH TLS` on `ftp://` when TLS is requested), sending curl 8.21.0's commands and returning its exit codes.

## Context

- BL-431 added `FtpProtocolHandler` as passive-mode, plaintext, `ftp`-only; ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`) records that scope and lists active mode and `ftps` as not implemented. Low priority: passive mode covers the common case.
- What is missing outside this project today, checked 2026-09-27: `-P/--ftp-port`, `--ssl`, `--ssl-reqd` and `--ftp-ssl*` are only names in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`, with no entry in `CommandLineOptionTable.cs` and no `ITransferContext` property; and `IConnector` (`Curl.Protocol.Abstractions.UnitLibrary/IConnector.cs`) can only connect out, with no way to listen for the server's active-mode data connection. `ITlsProvider.AuthenticateAsClientAsync` (`Curl.Protocol.Abstractions.UnitLibrary/ITlsProvider.cs`) already upgrades a plaintext `IConnection`, which is what `AUTH TLS` and a TLS data connection need; the handler would take an `ITlsProvider` in its constructor.
- This task's touches are the FTP project only. So the run first records an ADR deciding the scope and the contract additions (a listening seam for active mode, the option properties), then has `task-planner` file the prerequisite `Curl.Cli`/`Curl.Protocol.Abstractions`/`Curl.Networking`/`Curl.Console` tasks, adds them to this task's `depends-on`, and moves this task to `Blocked` on them (task-board skill, "Claiming and finishing", step 4). Once they are done, the handler work below completes the task.
- Measure with curl 8.21.0 before pinning. `Record-CurlExchange.ps1 -Ftp` only offers passive data connections and plaintext today; active mode needs the recorder to connect back to the address in `EPRT`/`PORT`, and TLS needs it to answer `AUTH TLS` and wrap the streams with `SslStream` using a test certificate. Extend the recorder rather than writing a server (root `CLAUDE.md`, "No Python"); that extension also touches `Record-CurlExchange.ps1`, so add it to `touches` when the task is unblocked.
- Match the platform's curl (root `CLAUDE.md`, "Decisions"): the Schannel build on Windows, the OpenSSL build on Linux and macOS; TLS failure texts may differ by platform and are pinned per platform. Upstream: https://curl.se/docs/manpage.html (`-P`, `--ssl`, `--ssl-reqd`, `--ftp-ssl-control`) and https://curl.se/libcurl/c/libcurl-errors.html.

## Acceptance criteria

- [x] An ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's delegation", records the scope of active mode and FTP TLS and the contract additions they need; the prerequisite tasks it names are filed and listed in this task's `depends-on`.
- [x] Named tests in `Curl.Protocol.Ftp.UnitTests` pin curl 8.21.0's command bytes for `-P -` (`EPRT`, then `PORT` when `EPRT` is refused) and the exit code and message when the server never connects back, as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [x] Named tests pin the `ftps://` implicit-TLS conversation and the `AUTH TLS` command and the protection commands after it that curl 8.21.0 sends (as measured) for `ftp://` with `--ssl-reqd`, and the exit code when `AUTH TLS` is refused under `--ssl-reqd`, using a fake `ITlsProvider` so no test touches the network.
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- 2026-09-27 (lane 2): ADR-0102 records the scope (`-P` with `-` or an address literal and
  an optional port range, `EPRT` then `PORT`, `--disable-eprt`; `ftps://`; `--ssl`,
  `--ssl-reqd`, `--ftp-ssl-control` with `PBSZ`/`PROT`) and the contract additions:
  `IConnectionListener`, `IConnection.LocalEndPoint` as a default interface member, and
  four `ITransferContext` options. Filed: BL-459 (abstractions, prerequisite, added to
  `depends-on`), BL-456 (networking listener), BL-457 (CLI parsing), BL-458 (console
  wiring, depends on this task). Only BL-459 blocks the handler work: the handler gains a
  constructor taking `IConnector`, `IConnectionListener` and `ITlsProvider`, and its tests
  use fakes, so BL-456/457 run in parallel and BL-458 follows. Waiting on work, not on
  Stewart, so the task goes to Backlog rather than Blocked (dark factory rule 4).
- `touches` gained `Documentation/Planning/Decisions` (the ADR and its index row; no task
  in Doing names it) and `Record-CurlExchange.ps1` (the recorder extension for active mode
  and TLS, as Context says).
- 2026-09-27 (lane 3): delivered. Measured curl 8.21.0 (Schannel) with the extended
  `Record-CurlExchange.ps1 -Ftp` (dials back to `EPRT`/`PORT`, answers `AUTH` and `PROT P`
  with TLS, `-Tls` with `-Ftp` for implicit FTPS, `-FtpIdleMilliseconds` for the 60-second
  accept wait). What was measured and every divergence is in ADR-0102's BL-437 addendum.
  New tests: `FtpProtocolHandlerActiveModeTests` (31) and `FtpProtocolHandlerTlsTests`
  (23); FTP tests 214 -> 268; `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`
  reports 100% line, 100% branch, worst CRAP 10, 0 failing members.
- Defaults taken: the one-argument constructor stays for `Curl.Console` until BL-458 and
  serves `ftp` only (`-P` there is exit 30, an accepted `AUTH` exit 64); a `-P` host or
  interface name ends with exit 6 and no `QUIT` (curl's ending for a name that does not
  resolve; resolving is BL-466); IPv6 with `EPRT` refused ends with exit 30 after `QUIT`
  where curl hangs; a failed data TLS handshake ends without `QUIT` (unmeasured, same as a
  failed passive connect); curl's watch on the control connection during the accept wait
  is not reproduced.
- Filed BL-465 (TLS connections from `TcpConnector` must report `LocalEndPoint`, or
  `-P -` over `ftps://` ends with exit 30) and BL-466 (`-P` host and interface names).

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Waits on BL-459 (IConnectionListener and the FTP active-mode/TLS transfer options in Curl.Protocol.Abstractions), per ADR-0102
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. FtpProtocolHandler does active mode (-P: EPRT, PORT, --disable-eprt) and TLS (ftps://, AUTH SSL/TLS, PBSZ/PROT) as curl 8.21.0 was measured to
