---
id: BL-1099
title: Know smb and smbs in --proto, --proto-redir and redirect targets now that they are served
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-598]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1099 — Know smb and smbs in --proto, --proto-redir and redirect targets now that they are served

## Goal

With `smb` and `smbs` served (BL-598), `--proto` and `--proto-redir` know them and a redirect to an `smb://` URL is parsed as a curl build with SMB parses it, on every platform.

## Context

- `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`'s `KnownSchemes` is the Windows Schannel build's list (ADR-0189), which has no SMB, so today `--proto -ftp smb://h/s/f` leaves `smb` out of the allowed set and refuses the transfer with `Protocol "smb" is disabled`, and `--proto smb` warns it is unrecognized.
- `Curl.Core.UnitLibrary/RedirectFollower.cs`'s `SchemesCurlParses` lacks `smb`/`smbs`, so a `Location: smb://...` fails as `Unsupported URL scheme` instead of reaching the `--proto-redir` check.
- Measure the Linux OpenSSL build (WSL curl, which has SMB) for both before pinning; amend ADR-0189 (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [x] Measured first with WSL curl; the output copied into Notes.
- [x] `CommandLineProtocolSetOptionTests` pin `--proto -ftp` allowing `smb` and `--proto smb` without a warning.
- [x] A `RedirectFollower` test pins a redirect to `smb://` reaching the `--proto-redir` refusal curl prints.
- [x] ADR-0189 amended; `--ai-help` checked for any scheme list it prints.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Delivered directly rather than through the full `/feature` agent stages: the change is two list
  entries in each of two libraries plus tests, with no new seam or code path to plan.
- Measured with WSL's Ubuntu curl 8.18.0 (OpenSSL 3.5.5) on 2026-10-01:
  - `curl -V` `Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt pop3 pop3s rtmp rtsp scp sftp smb smbs smtp smtps telnet tftp ws wss`
  - `curl --proto smb,bogus -o /dev/null http://127.0.0.1:1/` -> `Warning: unrecognized protocol 'bogus'` only, exit 7.
  - `curl -sS --proto -ftp smb://127.0.0.1:1/s/f` -> `curl: (7) Failed to connect to 127.0.0.1 port 1 after 0 ms: Could not connect to server`, exit 7 (smb still allowed).
  - `curl -sS --proto =http smb://127.0.0.1:1/s/f` -> `curl: (1) Protocol "smb" disabled`, exit 1.
  - `Record-CurlExchange.ps1 -Curl wsl.exe -ListenAddress 172.26.96.1`, `Location: smb://h/s/f`, `curl -sS -L [--proto-redir http] http://172.26.96.1:<port>/` -> `curl: (1) Protocol "smb" disabled (in redirect)`, exit 1, both with and without `--proto-redir` (curl's default redirect set is http, https, ftp, ftps).
- `KnownSchemes` and `SchemesCurlParses` gain `smb` and `smbs`; `--proto-default smb` is now taken too. Curl keeps curl 8.21.0's `is disabled` wording (8.18.0 omits `is`).
- ADR-0189 amended (Decided by Claude under Stewart's delegation): on Windows `--proto smb` is now silent where the Schannel curl warns, because Curl serves SMB there.
- `--ai-help` prints no scheme list (`CurlAiHelpText.cs`, `CurlManualMarkdown.cs` checked), so nothing to change.
- `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary,Curl.Core.UnitLibrary`: Cli 4577/4577 lines, 1313/1313 branches; Core 2946/2946 lines, 1293/1293 branches; 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --proto, --proto-redir and --proto-default know smb and smbs, and a redirect to smb:// reaches the --proto-redir refusal
