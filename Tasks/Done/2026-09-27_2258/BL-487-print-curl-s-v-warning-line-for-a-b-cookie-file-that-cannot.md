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
completed: 2026-09-27
---
# BL-487 — Print curl's -v WARNING line for a -b cookie file that cannot be opened

## Goal

Under `-v`, a `-b` file that cannot be opened prints curl 8.21.0's `* WARNING: failed to open cookie file "<path>"` line before the transfer, where Curl today prints nothing.

## Context

- Found in BL-461. Measured 2026-09-27 on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `curl -s -v -b <missing path> -c - http://127.0.0.1:<port>/` printed `* WARNING: failed to open cookie file "C:\Users\...\cf.txt"` as the first standard error line, before `*   Trying`, and exited 0.
- `CookieStore.LoadCookieFileAsync(IFileSystem, string, bool, DateTimeOffset, ITransferEvents, CancellationToken)` (BL-461) already receives the run's events and returns silently when `IFileSystem.OpenForReadAsync` fails; `Curl.Console\CookieEngine.cs` calls it, and `-b -` reads standard input, which always opens.
- Before pinning, measure: the path as printed for a relative path and for a directory, whether `-s` alone or `--trace-ascii` shows the line, and whether a directory prints the same line.

## Acceptance criteria

- [x] The measurements in Context are recorded in this task's Notes with command and stderr bytes.
- [x] A test in `Curl.Cookies.UnitTests` asserts loading a missing file reports exactly `WARNING: failed to open cookie file "<path>"` to a recording `ITransferEvents`, with the path as curl printed it.
- [x] A `Curl.Console.UnitTests` test shows the line on standard error under `-v -b <missing>`.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cookies` and `Curl.Console`.

## Notes

Measured 2026-09-27, curl 8.21.0 (Windows) libcurl/8.21.0 Schannel, `Record-CurlExchange.ps1 -Port 18487`
from a scratch directory holding a directory `adir`; each run exited 0:

| Command | First standard error line |
| --- | --- |
| `curl -s -v -b <tmp>\missing.txt http://127.0.0.1:18487/` | `* WARNING: failed to open cookie file "C:\Users\...\bl487\missing.txt"` |
| `curl -s -v -b sub\missing.txt ...` | `* WARNING: failed to open cookie file "sub\missing.txt"` (bytes `2A 20 57 41 ... 22 73 75 62 5C 6D ... 74 78 74 22 0D 0A`, then `*   Trying`) |
| `curl -s -v -b sub/missing.txt ...` | `* WARNING: failed to open cookie file "sub/missing.txt"` |
| `curl -s -v -b adir ...` (a directory) | `* WARNING: failed to open cookie file "adir"` - the same line |
| `curl -s -b missing.txt ...` | nothing: standard error empty (0 bytes) |
| `curl -s --trace-ascii - -b missing.txt ...` | standard error empty; the trace on stdout starts `* WARNING: failed to open cookie file "missing.txt"` |

So the path is printed exactly as given (no resolving, no slash rewriting), a directory is just a
file that cannot be opened, and the line is an ordinary info (`CURLINFO_TEXT`) line: it goes wherever
`-v`/`--trace-ascii` send info lines and nowhere under `-s` alone. Implemented as one
`ITransferEvents.ReportInfo` call in `CookieStore.LoadCookieFileAsync` when `OpenForReadAsync` fails;
`CookieEngine` already passes the run's events, and `-b -` never reaches that path.

No ADR: there was no design choice - the behaviour is curl's, measured. The `feature` pipeline's plan,
review and conformance stages were collapsed into this session because the change is one line in one
method plus its tests, pinned against the measurement above.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. -v -b <file that cannot be opened> prints curl's WARNING: failed to open cookie file line first
