---
id: BL-1432
title: Write curl's netrc 'Could not find host' -v line and check the URL scheme before the netrc lookup
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-04
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

- [x] Tests in `Curl.Console.UnitTests` pin, under `-v`, the line `* Could not find host 127.0.0.1 in the <file> file; using defaults` at the measured position for a `--netrc-file` with no matching entry, `.netrc` as the file name when no `--netrc-file` is given, the line under `--netrc-optional` with a missing file, and no line when the entry is found, when `-u user:password` skips the lookup, or without `-v`.
- [x] A test pins `-n` with a missing netrc file and an unsupported scheme (`qttp://x/`) failing with exit 1 and `curl: (1) Protocol "qttp" not supported`, not exit 26.
- [x] Test paths are drive-less so the tests pass on Windows, Linux and macOS.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

- Measured 2026-10-04 with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`:
  - `-v --netrc-file <file naming example.com> http://127.0.0.1:<port>/`: `* Could not find host 127.0.0.1 in the <path as given> file; using defaults` is the first stderr line, before `*   Trying 127.0.0.1:<port>...`; exit 0.
  - `-v --netrc-optional http://LOCALHOST:<port>/` with `HOME`/`USERPROFILE` an empty directory: `* Could not find host LOCALHOST in the .netrc file; using defaults`, before `* Host LOCALHOST:<port> was resolved.` The host keeps the URL's case, as `CurlUrl.Host` does.
  - A matching entry, `-u u:p`, and no `-v` write no line. `-n` with the file missing writes `* .netrc error: no such file` before `curl: (26) ...` - not written by Curl today, filed as BL-1447 (the script gave BL-1441, already taken on the shared branch, so it was renumbered).
  - `--netrc-optional --netrc-file <missing file>` is refused by the tool's parser (exit 2), so the missing-file test uses the default `.netrc`.
  - `-n qttp://x/`: `curl: (1) Protocol "qttp" not supported`, exit 1; under `-v` preceded by `* Protocol "qttp" not supported`.
- Design: `ProtocolDispatcher.Serves(scheme)` (new, Curl.Core) is checked first in `CurlCommandRunner.TrySelectProxyAndCredentials`, before the proxy and the netrc lookup, as curl resolves the scheme in `parseurlandfillconn` before `create_conn` picks the proxy and `override_login` reads the netrc file. The line goes through `EventsBeforeConnecting(transfer).ReportInfo`, passed to `TransferCredentialLookup.TryLookUp` as `reportInfo`; redirect hops pass none, so a hop writes no line (not measured).
- `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests` added to `touches` for `Serves`: the dispatcher held the only list of served schemes. No other task in Doing on `origin/work/dark-factory` (BL-1430 Http, BL-1441 Conformance, BL-1444 Tftp) names them.
- `TryLookUp` reached complexity 16 with the line; its netrc-file part moved to `TryLookUpInNetrcFile`. `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Core.UnitLibrary`: both 100% line and branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-04: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Done. curl -v writes 'Could not find host <host> in the <file> file; using defaults' and an unsupported scheme fails with exit 1 before the netrc lookup
