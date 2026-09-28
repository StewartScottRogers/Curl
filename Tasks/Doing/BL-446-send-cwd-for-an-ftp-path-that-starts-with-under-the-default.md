---
id: BL-446
title: Send CWD / for an ftp:// path that starts with // under the default multicwd method
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-27
completed:
---
# BL-446 — Send CWD / for an ftp:// path that starts with // under the default multicwd method

## Goal

`curl ftp://host//abs/f.txt` (default `--ftp-method multicwd`) sends `CWD /` then `CWD abs`, as curl 8.21.0 does, instead of today's `CWD abs` alone.

## Context

- Found while measuring BL-436: `Record-CurlExchange.ps1 -Ftp -CurlArgs ftp://127.0.0.1:<port>//abs/f.txt` shows curl 8.21.0 sending `CWD /`, `CWD abs`, then `EPSV`, `TYPE I`, `SIZE f.txt`, `RETR f.txt`, `QUIT`. `FtpUrlPath.Parse` (`Curl.Protocol.Ftp.UnitLibrary/FtpUrlPath.cs`) skips every empty segment under `multicwd`, so the leading `/` is lost. ADR-0093's BL-436 addendum records the divergence.
- While there, measure how curl's `multicwd` treats an empty segment in the middle (`/a//b/f.txt`) and a `%2F` inside a segment (curl decodes the whole path before splitting it), and match both.
- `singlecwd` and `nocwd` already follow curl here (BL-436); leave them as they are.

## Acceptance criteria

- [ ] A named test in `Curl.Protocol.Ftp.UnitTests` pins `CWD /` then `CWD abs` for `ftp://host//abs/f.txt`, and the measured commands for `/a//b/f.txt` and `/a%2Fb/f.txt`, each as recorded with `Record-CurlExchange.ps1 -Ftp` against curl 8.21.0.
- [ ] ADR-0093's BL-436 addendum's note about the divergence is replaced by what was measured.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports no failing member.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
