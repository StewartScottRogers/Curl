---
id: BL-523
title: Refuse schemes --proto excludes and redirects --proto-redir excludes
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-522]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-523 — Refuse schemes --proto excludes and redirects --proto-redir excludes

## Goal

A URL whose scheme `--proto` excludes is refused before any connection, and a `-L` redirect to a scheme `--proto-redir` excludes (by default anything but HTTP, HTTPS, FTP and FTPS) is refused, each with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 10 (Blocker). Parsing is BL-522.
- Scheme dispatch: `Curl.Core.UnitLibrary/ProtocolDispatcher.cs`; redirects: `RedirectFollower.cs`, `RedirectPolicy.cs`, and `Curl.Console/RedirectPolicyMapping.cs`.
- Whether the default redirect set applies even without `--proto-redir` today (for example a redirect to `file://`) must be measured; if Curl already refuses such redirects, keep one code path.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--proto =https http://...`, `--proto -http http://...`, `-L` with `Location: file:///dir/x`, `Location: dict://127.0.0.1/x` with and without `--proto-redir =http,dict`; stderr and exit code copied into Notes.
- [ ] `Curl.Core.UnitTests` pin the refusal for each measured case; `Curl.Console.UnitTests` pin one of each end to end.
- [ ] New tests are platform-neutral (drive-less `file:///dir/x`).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
