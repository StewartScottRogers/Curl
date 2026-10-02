---
id: BL-598
title: Register the SMB handler for smb and smbs in Curl.Console with its -v lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-597]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-598 — Register the SMB handler for smb and smbs in Curl.Console with its -v lines

## Goal

On Windows, Linux and macOS, `curl smb://...` and `curl smbs://...` run end to end through `Curl.Console` with the `-v` lines of a curl 8.21.0 build that has SMB, as BL-594's ADR states.

## Context

- Conformance audit 2026-09-28, row 39. Handler: BL-595 to BL-597. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): offered on every platform, no platform check.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` accepts `smb`/`smbs` (port 445); the `-V` protocol list follows ADR-0021.
- Measure `-v` for a download against Samba as in BL-595.

## Acceptance criteria

- [x] Measured first as above; stderr copied into Notes with varying parts marked.
- [x] `Curl.Console.UnitTests` pin an `smb://` and an `smbs://` download end to end on every platform.
- [x] `curl -V` lists `smb` and `smbs` on every platform, with tests.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- **Measured 2026-10-01** as BL-595/596 did (the Windows Schannel build has no SMB): Ubuntu's curl
  8.18.0 (OpenSSL, WSL) via `Record-CurlExchange.ps1 -Script <file> -Curl wsl.exe -ListenAddress
  172.26.96.1 -Port 14450 -CurlArgs '-e','curl','-sS','-v','-m','8','-u','User:Password',
  'smb://172.26.96.1:14450/share/dir/x.txt'`, the script serving `SmbRecordedExchange`'s replies.
  Varying parts in <angle brackets>:
  ```
  *   Trying 172.26.96.1:<port>...
  * Established connection to 172.26.96.1 (172.26.96.1 port <port>) from <local ip> port <local port>
  { [11 bytes data]
  * shutting down connection #0
  ```
  stdout `hello world`, exit 0. `smbs` with `-k` against `-Tls`: the same SMB lines after the
  OpenSSL handshake lines the connector owns (no `{ [11 bytes data]` line was shown there; left to
  the TLS layer). Upload (`-T`): Trying, Established, `* shutting down connection #0`, no data line.
  No user: `* closing connection #0`, `curl: (67) Login denied`. Too-small frame at the
  negotiate: `* too small NetBIOS frame size 5`, `* closing connection #0`, exit 56. Missing file:
  `* shutting down connection #0`, exit 78. `-T -`: `* SMB upload needs to know the size up front`,
  `* shutting down connection #0`, exit 55. No share in the path: `* missing share in URL path for
  SMB`, `* closing connection #-1`, exit 3.
- **Decision** (ADR-0315, Decided by Claude under Stewart's delegation): a `failf` text is a `-v`
  line, `curl_easy_strerror`'s texts are not; a failure before the session is set up ends
  `closing connection #N`, anything after it `shutting down connection #N` (libcurl's
  `multi_done`, premature only in the connect phase); a pre-connect refusal ends `#-1`.
- **touches** gained `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests`: `-V`'s `Protocols:` line is
  `CurlVersionText.ProtocolsLine` there. No task in Doing on `origin/work/dark-factory` names them
  (only BL-1098, SSH).
- **Follow-up filed:** BL-1099 — `--proto`'s `KnownSchemes` (ADR-0189, the Windows list) and
  `RedirectFollower.SchemesCurlParses` still lack `smb`/`smbs`.
- No option was added or changed, so `--ai-help` is unaffected.
- Quality: `Measure-CodeQuality.ps1` reports 100% line and branch, 0 failing members for
  `Curl.Protocol.Smb.UnitLibrary` (94 members, worst CRAP 8), `Curl.Console` (818) and
  `Curl.Cli.UnitLibrary` (912). `dotnet build Curl.slnx -warnaserror` clean; fast tests green in all
  32 test projects (Smb 111, Console 2038, Cli 3225).

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. curl smb:// and smbs:// run end to end through Curl.Console with curl's -v lines, and -V lists smb and smbs
