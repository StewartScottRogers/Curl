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
completed:
---
# BL-1099 — Know smb and smbs in --proto, --proto-redir and redirect targets now that they are served

## Goal

With `smb` and `smbs` served (BL-598), `--proto` and `--proto-redir` know them and a redirect to an `smb://` URL is parsed as a curl build with SMB parses it, on every platform.

## Context

- `Curl.Cli.UnitLibrary/CommandLineProtocolSet.cs`'s `KnownSchemes` is the Windows Schannel build's list (ADR-0189), which has no SMB, so today `--proto -ftp smb://h/s/f` leaves `smb` out of the allowed set and refuses the transfer with `Protocol "smb" is disabled`, and `--proto smb` warns it is unrecognized.
- `Curl.Core.UnitLibrary/RedirectFollower.cs`'s `SchemesCurlParses` lacks `smb`/`smbs`, so a `Location: smb://...` fails as `Unsupported URL scheme` instead of reaching the `--proto-redir` check.
- Measure the Linux OpenSSL build (WSL curl, which has SMB) for both before pinning; amend ADR-0189 (Decided by Claude under Stewart's delegation).

## Acceptance criteria

- [ ] Measured first with WSL curl; the output copied into Notes.
- [ ] `CommandLineProtocolSetOptionTests` pin `--proto -ftp` allowing `smb` and `--proto smb` without a warning.
- [ ] A `RedirectFollower` test pins a redirect to `smb://` reaching the `--proto-redir` refusal curl prints.
- [ ] ADR-0189 amended; `--ai-help` checked for any scheme list it prints.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
