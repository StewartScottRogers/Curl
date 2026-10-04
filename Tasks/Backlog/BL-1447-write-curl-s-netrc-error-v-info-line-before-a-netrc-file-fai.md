---
id: BL-1447
title: Write curl's '.netrc error' -v info line before a netrc file failure
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1447 — Write curl's '.netrc error' -v info line before a netrc file failure

## Goal

Under `-v`, a transfer that a required netrc file fails with exit 26 writes curl 8.21.0's `* .netrc error: <reason>` info line before its `curl: (26) .netrc error: <reason>` line.

## Context

- Measured 2026-10-04 (BL-1432 Notes) with curl 8.21.0 (mingw, Schannel): `curl -v -n http://127.0.0.1:<port>/` with `HOME` and `USERPROFILE` an empty directory writes `* .netrc error: no such file` then `curl: (26) .netrc error: no such file`, exit 26. Curl writes only the `curl: (26)` line.
- Upstream `lib/url.c` (tag `curl-8_21_0`), `override_login`: `failf(data, ".netrc error: %s", ...)`; `failf` writes the message as a `-v` info line as well.
- Curl: `Curl.Console/TransferCredentialLookup.cs` (`TryLookUp`, `FailureOf`) returns the exit-26 failure; `CurlCommandRunner.TrySelectProxyAndCredentials` already writes the control-code refusal's info line through `EventsBeforeConnecting(transfer).ReportInfo` - extend that to every netrc-file failure. Measure the syntax-error case (`* .netrc error: syntax error`?) with `Record-CurlExchange.ps1` before pinning it.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` pin, under `-v`, `* .netrc error: no such file` before `curl: (26) .netrc error: no such file` for `-n` with no netrc file, and the measured lines for a malformed file; without `-v` only the `curl: (26)` line.
- [ ] Test paths are drive-less so the tests pass on Windows, Linux and macOS.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
