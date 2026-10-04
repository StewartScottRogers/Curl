---
id: BL-1432
title: Write curl's netrc 'Could not find host' -v line and check the URL scheme before the netrc lookup
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1432 — Write curl's netrc 'Could not find host' -v line and check the URL scheme before the netrc lookup

## Goal

Under `-v`, a transfer that looks in a netrc file and finds no entry for its host writes curl 8.21.0's `* Could not find host <host> in the <file> file; using defaults` line, and a URL whose scheme Curl does not support fails with exit 1 before any netrc lookup can fail it with exit 26, as curl does.

## Context

- Upstream (tag `curl-8_21_0`), `lib/url.c` lines 1454-1474 (`override_login`): after `Curl_netrc_scan`, when the result is `NETRC_NO_MATCH`, or any failure under `--netrc-optional` (a missing file included), curl writes `infof("Could not find host %s in the %s file; using defaults", hostname, netrc_file ? netrc_file : ".netrc")` - the `--netrc-file` value as given, or the literal `.netrc` when none was given - and carries on; any other failure under `-n` is `.netrc error: <reason>`, exit 26. The lookup is skipped entirely when `-u` gives both user and password.
- Curl today: `Curl.Console/TransferCredentialLookup.cs` `TryLookUp` looks the host up (`NetrcFile.Find`, `NetrcLookupOutcome.NotFound`) and writes no line. `CurlCommandRunner.TrySelectProxyAndCredentials` already writes a credential `-v` line through `EventsBeforeConnecting(transfer).ReportInfo` (BL-1411); the new line goes the same way, at the position measured below.
- Ordering bug, upstream test 760 (`Curl.Conformance.UnitTests/UpstreamTestData/test760.rawhttp`): `curl -no1 -no2 --url "Qttp://internal.dxample.lol/status" -: --url "http"` exits 1 (`curl: (1) Protocol "qttp" not supported`) with real curl 8.21.0 (checked 2026-10-04 on Windows), but Curl exits 26 (`curl: (26) .netrc error: no such file`), because the netrc lookup runs before the scheme is checked. In curl the scheme is resolved in `parseurlandfillconn`, before `override_login`.
- Measure the line's position and host spelling first: `Record-CurlExchange.ps1 -CurlArgs "-v,--netrc-optional,--netrc-file,<file naming another host>,http://127.0.0.1:<port>/"`, and the same with `-n` and no `--netrc-file` (an empty `HOME`/`USERPROFILE` directory, so `.netrc` is missing), and a URL with an upper-case host. Record which line it comes before (expected: before `Trying`/`Connected` lines) in Notes.

## Acceptance criteria

- [ ] Tests in `Curl.Console.UnitTests` pin, under `-v`, the line `* Could not find host 127.0.0.1 in the <file> file; using defaults` at the measured position for a `--netrc-file` with no matching entry, `.netrc` as the file name when no `--netrc-file` is given, the line under `--netrc-optional` with a missing file, and no line when the entry is found, when `-u user:password` skips the lookup, or without `-v`.
- [ ] A test pins `-n` with a missing netrc file and an unsupported scheme (`qttp://x/`) failing with exit 1 and `curl: (1) Protocol "qttp" not supported`, not exit 26.
- [ ] Test paths are drive-less so the tests pass on Windows, Linux and macOS.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
