---
id: BL-437
title: Active mode (-P/--ftp-port) and ftps:// / AUTH TLS for FtpProtocolHandler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-431]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-27
completed:
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

- [ ] An ADR under `Documentation/Planning/Decisions/`, marked "Decided by Claude under Stewart's delegation", records the scope of active mode and FTP TLS and the contract additions they need; the prerequisite tasks it names are filed and listed in this task's `depends-on`.
- [ ] Named tests in `Curl.Protocol.Ftp.UnitTests` pin curl 8.21.0's command bytes for `-P -` (`EPRT`, then `PORT` when `EPRT` is refused) and the exit code and message when the server never connects back, as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [ ] Named tests pin the `ftps://` implicit-TLS conversation and the `AUTH TLS` command and the protection commands after it that curl 8.21.0 sends (as measured) for `ftp://` with `--ssl-reqd`, and the exit code when `AUTH TLS` is refused under `--ssl-reqd`, using a fake `ITlsProvider` so no test touches the network.
- [ ] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
