---
id: BL-1302
title: Write --trace-time stamps to the millisecond on Windows, as curl's GetSystemTime clock does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1301]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1302 — Write --trace-time stamps to the millisecond on Windows, as curl's GetSystemTime clock does

## Goal

On Windows, every `--trace-time` stamp Curl writes has its six fractional digits as curl 8.21.0's do there, the milliseconds followed by `000` (`22:57:08.626000 `); on Linux and macOS the stamp keeps full microseconds, as curl's `gettimeofday` gives them.

## Context

- curl 8.21.0 `src/tool_cb_dbg.c` (tag `curl-8_21_0`) lines 147-150: the stamp is `"%s.%06ld "` of `hms_for_sec(tv.tv_sec)` and `tv.tv_usec` from `tvrealnow()`. `src/tool_util.c` lines 28-47 (`#ifdef _WIN32`): `tvrealnow` reads `GetSystemTime`, whose resolution is the millisecond, and sets `tv_usec = systime.wMilliseconds * 1000`; lines 51-61 (every other platform): `gettimeofday`, full microseconds.
- Measured on 2026-10-02 against curl 8.21.0 (mingw64, Windows) with `Record-CurlExchange.ps1` and `-s -o NUL --trace-ascii - --trace-time --trace-ids http://127.0.0.1:PORT/`: every stamp ends in `000` (`22:57:08.626000 [0-0] *   Trying ...`, `22:57:08.628000 [0-0] * Established ...`, `22:57:08.634000 [0-0] <= Recv header, 17 bytes (0x11)`); Curl's run printed `22:57:08.963531 [0-0] ...`, `22:57:08.994258 ...`.
- Curl today: `Curl.Output.UnitLibrary/TraceTimeStamp.cs` `Read` takes `TimeProvider.GetLocalNow()` and prints `Ticks / TicksPerMicrosecond % 1_000_000`, on every platform. Its own doc comment's example, `03:30:30.939000 `, is already a Windows-shaped stamp. Callers: `TraceTransferEventWriter` (line 243) and `VerboseTransferEventWriter` (line 280).
- Keep the platform choice testable on every OS: let `TraceTimeStamp` take whether to truncate (defaulting from `OperatingSystem.IsWindows()` at the composition edge), so both branches run in CI on Windows, Linux and macOS without `[OSCondition]`; pin the default per platform with `[OSCondition]` tests.

## Acceptance criteria

- [ ] Tests in `Curl.Output.UnitTests` with a fixed `TimeProvider` at `03:30:30.939512` local time assert `03:30:30.939000 ` when truncating to the millisecond and `03:30:30.939512 ` when not.
- [ ] A test marked `[OSCondition(OperatingSystems.Windows)]` asserts the default stamp is truncated, and one marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]` asserts it is not.
- [ ] Tests through `TraceTransferEventWriter` and `VerboseTransferEventWriter` with timestamps on pin that both writers use the same rule.
- [ ] `dotnet build Curl.Output.UnitTests -warnaserror` is clean; `dotnet test Curl.Output.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports no failing member.

## Notes

- If the writers are constructed in `Curl.Console` with no way to pass the choice, default it inside `Curl.Output` from `OperatingSystem.IsWindows()` rather than touching `Curl.Console`, which is outside this task.

## Log

- 2026-10-02: Created.
