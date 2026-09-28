---
id: BL-434
title: Register FtpProtocolHandler for non-proxied ftp:// in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-431, BL-438, BL-439]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-434 — Register FtpProtocolHandler for non-proxied ftp:// in Curl.Console

## Goal

`Curl.Console ftp://host/path/file` runs the transfer through `FtpProtocolHandler` and prints curl 8.21.0's stdout, stderr and exit code, while an `ftp://` URL forwarded through an HTTP proxy still goes through `ForwardedFtpProtocolHandler` and the HTTP handler.

## Context

- BL-431 added `FtpProtocolHandler(IConnector connector)` in `Curl.Protocol.Ftp.UnitLibrary`; ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`) records its conversation and says, under Consequences, that it is registered in `Curl.Console` by a separate task once it honours `Range`, `ResumeFrom`, `Upload` and `NoBody`. BL-438 (`-r`, `-C`, `-I`) and BL-439 (`-T`) do that, so this task depends on both.
- Today `Curl.Console/CurlComposition.cs` (`CreateProtocolHandlers`, around line 58) registers `new ForwardedFtpProtocolHandler(http)` as the only `ftp` handler. `Curl.Console/ForwardedFtpProtocolHandler.cs` hands a transfer to the HTTP handler when `context.Http` has a `ForwardProxy` of kind `ProxyKind.Http` or `ProxyKind.Http10` with `ProxyTunnel: false`, and otherwise fails with exit 1 (`CurlExitCode.UnsupportedProtocol`) and `Protocol "ftp" not supported`.
- Two handlers cannot both claim `ftp` in `SupportedSchemes`, so the simplest wiring is to give `ForwardedFtpProtocolHandler` the `FtpProtocolHandler` as the handler for every transfer it does not forward (replacing the exit-1 branch), constructed with the same `connector` the other TCP handlers get. Update its XML doc comment and name if its behaviour no longer matches "forwarded" (root `CLAUDE.md`, "Say what it does, do what it says").
- `Curl.Console.csproj` already references `Curl.Protocol.Ftp.UnitLibrary`.
- Tests stay off the network: use the fake connectors already in `Curl.Console.UnitTests` (see `ForwardedFtpProtocolHandlerTests.cs` and `CurlCompositionTests.cs`).
- Measure the end-to-end check with `Record-CurlExchange.ps1 -Ftp -FtpData 'hello'` against curl 8.21.0 and against the built `Curl.Console`, and record both results in this task's Log.

## Acceptance criteria

- [x] A test in `Curl.Console.UnitTests/ForwardedFtpProtocolHandlerTests.cs` (or the renamed class's test file) shows a non-proxied `ftp://` transfer reaching `FtpProtocolHandler`: with a fake connector scripting the ADR-0093 conversation, the output bytes equal the served file and the result is exit 0.
- [x] Existing tests that pin forwarding through an HTTP or HTTP/1.0 proxy without `--proxytunnel` still pass unchanged.
- [x] No test in `Curl.Console.UnitTests` expects `Protocol "ftp" not supported` for a non-proxied `ftp://` URL any longer; any such test is updated to the FTP handler's result.
- [x] `Record-CurlExchange.ps1 -Ftp -FtpData 'hello'` run once with curl 8.21.0 and once with `Curl.Console` (`-Curl <path to built Curl.Console>`) on `ftp://127.0.0.1:<port>/file.txt` gives the same stdout bytes and exit code, and both outputs are noted in this task's Log.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Console` this task changes.

## Notes

- Plan: `ForwardedFtpProtocolHandler` now takes the FTP handler as its second dependency and
  hands it every transfer it does not forward, replacing the exit-1 branch. Its name no longer
  said what it does, so it is renamed `RoutingFtpProtocolHandler` (file and test file with it).
  `CurlComposition` builds it with `new FtpProtocolHandler(connector)`, the same pooling
  connector every other TCP handler gets.
- `CurlCompositionProxyTests.RunAsync_FtpUrlNotForwardedThroughAnHttpProxy_*` (no proxy, and
  `-p`) used to pin exit 1; it now pins the FTP handler connecting to `example.com:21` (not as a
  forward proxy) and returning the connector's exit 7.
- Added `Documentation/Planning/Decisions/ADR-0093-...md` to `touches`: its Consequences said
  the handler was not registered yet, which this task makes false. No task in `Doing` names it.
- Updated `Curl.Console/CLAUDE.md` for the rename. `Tasks/Backlog/BL-392` still names
  `ForwardedFtpProtocolHandler` in its Context; left for that task, which owns its own file.
- Measure-CodeQuality on Curl.Console: every member this task changed is 100%/100%; the one
  failing member, `DiskWriteOutFileOpener.TryOpen`, predates this task and is BL-432.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Record-CurlExchange.ps1 -Ftp -FtpData 'hello' on ftp://127.0.0.1:<port>/file.txt: curl 8.21.0 stdout `hello`, exit 0, commands USER/PASS/PWD/EPSV/TYPE I/SIZE/RETR/QUIT; Curl.Console (Curl.Console\bin\Debug\net10.0\curl.exe) stdout `hello`, exit 0, the same commands.
- 2026-09-27: Doing -> Done. Curl.Console downloads non-proxied ftp:// through FtpProtocolHandler, matching curl 8.21.0; HTTP-proxy forwarding unchanged
