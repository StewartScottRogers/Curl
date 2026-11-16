---
id: BL-5263
title: Keep the -v DNS cache lines of good --resolve entries when a malformed entry exits 49 ahead of a rejected URL
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-5263 — Keep the -v DNS cache lines of good --resolve entries when a malformed entry exits 49 ahead of a rejected URL

## Goal

With `-v`, a run whose malformed `--resolve` entry exits 49 ahead of a rejected URL prints the same `Added <host>:<port>:<address> to DNS cache` lines for the good entries before the bad one that curl prints.

## Context

Shift-end review of 717cc1483a..7877076cd1 (ADR-0710); found by the silent-failure-hunter.

- `Curl.Console/CurlCommandRunner.cs:3583` and `:3592` (`TryParseQueriedUrl`) now return `MalformedResolveFailure(options)` (around line 3600) when a `--resolve` entry is malformed and the URL is rejected (AF-0807).
- `MalformedResolveFailure` builds `TransferResult.Failure(CurlExitCode.SetoptOptionSyntax, parseError)` and reports no events.
- The normal exit-49 path is `TcpConnector.ConnectAndNumberAsync` (`Curl.Networking.UnitLibrary/TcpConnector.cs:609`). It first calls `LoadResolveEntriesUnlessLoaded(target.Events)`, which writes the `-v` `Added ... to DNS cache` lines for the entries that parse ahead of the bad one, and only then fails with 49.
- curl's `Curl_loadhostpairs` runs in `Curl_pretransfer`, before the URL is parsed. That is why AF-0807 found 49 ahead of the URL error, and it also means curl prints those `-v` lines on this path.
- Example: `curl -v --resolve good:80:127.0.0.1 --resolve x: /` drops the `* Added good:80:127.0.0.1 to DNS cache` line that curl prints. Whatever `-v` text curl prints for this path has to be measured before it is pinned.

## Acceptance criteria

- [x] Real curl 8.22.0 (the Schannel build on Windows) is measured with `Record-CurlExchange.ps1` for `-v --resolve good:80:127.0.0.1 --resolve x: /` and for the `-G --url-query a=b ... "http://a b/"` form. Its stderr is recorded in the task log.
- [x] Curl's stderr for both command lines matches the measured bytes, including any `Added ... to DNS cache` lines for the good entries, and the exit code is still 49.
- [x] `Curl.Console.UnitTests/CurlCommandRunnerMalformedResolveTests.cs` has a test with `-v` and a good entry before the bad one. The test pins the measured lines.
- [x] `dotnet build` is clean, the fast tests pass, and `Curl.Console` stays at 100% line and branch coverage.

## Notes

If the measurement shows curl prints no `Added` line here, record that in the log, add the pinning test, and close the task without a product change.

## Log

- 2026-11-15: Created by the shift-end review (ADR-0710).
- 2026-11-15: Backlog -> Doing. Criteria at claim: 79a3ef5a 9bfda06c ebfb1d10 1c8244dd.
- 2026-11-15: Measured real curl 8.22.0 (Schannel, a debug libcurl build, so its lines read `* [DNS] added good:80:127.0.0.1 to cache`): `-v --resolve good:80:127.0.0.1 --resolve x: /` prints that line, then `* Could not parse CURLOPT_RESOLVE entry 'x:'`, then `curl: (49) Could not parse CURLOPT_RESOLVE entry 'x:'`, exit 49. Curl already printed the added line (the dispatch loads the entries before the URL parse); the info line was missing, now reported by `MalformedResolveFailure`. `-G --url-query a=b ... "http://a b/"` with or without a bad entry: curl exits 3 `Could not parse the URL, failed to set query` (the tool fails the query before libcurl loads entries); BL-5258 had wrongly given 49, fixed. Tests pin both. Curl.Console quality run: 0 failing members. Not done: the same info line on the normal (valid URL) exit-49 path in TcpConnector, outside touches.
- 2026-11-15: Doing -> Done. -v good entries: Added line already printed; entry info line added; -G rejection now exit 3 as curl
