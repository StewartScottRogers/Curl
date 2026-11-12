---
id: BL-5016
title: Stop the run with exit 3 when -O cannot extract a file name from the URL
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Console.UnitTests/CurlCommandRunnerRemoteNameExtractionFailureTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5016 — Stop the run with exit 3 when -O cannot extract a file name from the URL

## Goal

When `-O` (or `--remote-name-all`, or the `-OJ` file name) cannot yield a file name from the URL, the whole run stops with REF's message and exit 3 (or the URL's own error code for a bad scheme) instead of going on to later URLs and exiting 0.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-032 (Medium, Confirmed). Silent wrong operation: the failure is hidden by exit 0 and later URLs run where REF would stop.

- **Repro:** serve a directory with `f/good.txt`; in an empty directory:
  1. `curl -sS -O "http://127.0.0.1:41000/f/a b" -O http://127.0.0.1:41000/f/good.txt; echo rc=$?; ls`
  2. `curl -sS --remote-name-all "http://127.0.0.1:41000/f/a b" http://127.0.0.1:41000/f/good.txt; echo rc=$?; ls`
  3. `curl -sS -O "foo://x/z" -O http://127.0.0.1:41000/f/good.txt; echo rc=$?`
  4. `curl -sS -O "http://127.0.0.1:41000/f/a b"; echo rc=$?`
- **Expected (REF):** commands 1, 2 and 4 print `curl: Failed to extract a filename from the URL to use for storage`, then `curl: (3) URL using bad/illegal format or missing URL`; exit 3; the second URL is not fetched and no file is written. Command 3 prints the same first line, then `curl: (1) Unsupported protocol`; exit 1. A `-OJ` response with `Content-Disposition: attachment; filename=" "` also gives the "Failed to extract" line and the `(3)` line.
- **Actual (PORT):** commands 1 and 2 print `curl: (3) URL rejected: Malformed input to a URL function`, then fetch and save `good.txt`, exit 0. Command 3 prints `curl: (1) Protocol "foo" not supported` and exits 0. Command 4 exits 3 with a different single line. The `filename=" "` case prints `curl: (3) URL rejected: Malformed input to a URL function`.
- **Where to start:** `Curl.Console/CurlCommandRunner.cs`, `ResolveOutputFile` and `RemoteNameUrlPath` (about lines 4548-4610), which treat a failed name extraction as a per-URL error. REF's wording already exists as `IpfsRemoteNameMessage` (line 594). A failed remote-name extraction should print that message and the `(3)` line, then end the whole run with exit 3, or with the URL's own error code for a bad scheme.

## Acceptance criteria

- [ ] A new test file `Curl.Console.UnitTests/CurlCommandRunnerRemoteNameExtractionFailureTests.cs` (using the existing runner test harness and fake connections) asserts that `-O "http://host/f/a b" -O http://host/f/good.txt` writes `curl: Failed to extract a filename from the URL to use for storage` then `curl: (3) URL using bad/illegal format or missing URL`, returns exit 3, makes no request for the second URL and writes no file.
- [ ] The same assertions hold for `--remote-name-all` with two URLs and for a single `-O "http://host/f/a b"`.
- [ ] `-O "foo://x/z" -O http://host/f/good.txt` prints the "Failed to extract" line then `curl: (1) Unsupported protocol` and returns exit 1 without fetching the second URL.
- [ ] A `-OJ` response with `Content-Disposition: attachment; filename=" "` gives the "Failed to extract" line, the `(3)` line and exit 3.
- [ ] Regression: `-O http://host/f/good.txt` and `-O` with several valid URLs still save each file and exit 0.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. The IPFS remote-name path (`IpfsRemoteNameMessage`) must keep its current behaviour.

## Log

- 2026-11-12: Created.
