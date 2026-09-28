---
id: BL-487
title: Print curl's -v WARNING line for a -b cookie file that cannot be opened
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-461]
touches: [Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-487 — Print curl's -v WARNING line for a -b cookie file that cannot be opened

## Goal

Under `-v`, a `-b` file that cannot be opened prints curl 8.21.0's `* WARNING: failed to open cookie file "<path>"` line before the transfer, where Curl today prints nothing.

## Context

- Found in BL-461. Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `curl -s -v -b <missing path> -c - http://127.0.0.1:<port>/` printed `* WARNING: failed to open cookie file "C:\Users\...\cf.txt"` as the first standard error line, before `*   Trying`, and exited 0.
- `CookieStore.LoadCookieFileAsync(IFileSystem, string, bool, DateTimeOffset, ITransferEvents, CancellationToken)` (BL-461) already receives the run's events and returns silently when `IFileSystem.OpenForReadAsync` fails; `Curl.Console\CookieEngine.cs` calls it, and `-b -` reads standard input, which always opens.
- Before pinning, measure: the path as printed for a relative path and for a directory, whether `-s` alone or `--trace-ascii` shows the line, and whether a directory prints the same line.

## Acceptance criteria

- [ ] The measurements in Context are recorded in this task's Notes with command and stderr bytes.
- [ ] A test in `Curl.Cookies.UnitTests` asserts loading a missing file reports exactly `WARNING: failed to open cookie file "<path>"` to a recording `ITransferEvents`, with the path as curl printed it.
- [ ] A `Curl.Console.UnitTests` test shows the line on standard error under `-v -b <missing>`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies` and `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
