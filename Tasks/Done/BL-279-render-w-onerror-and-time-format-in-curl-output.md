---
id: BL-279
title: Render -w %{onerror} and %time{format} in Curl.Output
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-224]
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Documentation/Planning/Decisions/ADR-0038-w-time-follows-the-windows-c-runtime-strftime-and-onerror-reads-transfer-failed.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-279 — Render -w %{onerror} and %time{format} in Curl.Output

## Goal

`WriteOutTemplateRenderer` stops at `%{onerror}` after a successful transfer and renders `%time{format}` as curl 8.21.0 does.

## Context

- Found by BL-224. curl 8.21.0 renders `-w "%time{%Y}Q"` as the year then `Q` (measured 2026-09-26); today the renderer writes `%time{%Y}Q` literally, because BL-224 covered only the directives it named.
- `%{onerror}` ends the output when the transfer succeeded (curl `src/tool_writeout.c`, `VAR_ONERROR`); the renderer needs to know whether the transfer failed, which `IWriteOutVariableSource` does not yet say.
- %time needs an injected `TimeProvider`; measure the strftime subset curl supports, including `%f` for microseconds, before pinning.

## Acceptance criteria

- [x] `%{onerror}` after a successful transfer writes nothing further and after a failed one continues (measured on curl 8.21.0, pinned in a test).
- [x] `%time{...}` output for the measured formats matches curl 8.21.0 with a fake `TimeProvider`.
- [x] `dotnet build Curl.Output.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Output`.

## Notes

- Touches widened to the new ADR-0038 and the Decisions index: the dialect and locale choice needed an ADR. No task in Doing named `Documentation`.
- Plan, done directly (one library and its tests): new `WriteOutTimeFormatter` (a dictionary per conversion keeps each method under complexity 10); `WriteOutTemplateRenderer` takes a `TimeProvider` and recognises `%time{`; `IWriteOutVariableSource.TransferFailed` (from `TransferResult.IsSuccess`) drives `%{onerror}`.
- Measured with curl 8.21.0 (mingw, Schannel) on 2026-09-26, en-US, US Mountain Standard Time:
  - `curl -s -o NUL -w "[%time{%<c>}]" file:///c:/Windows/win.ini` for every letter: the Microsoft C runtime set (`a A b B c d H I j m M p s S U w W x X y Y %`) plus curl's own `f z Z`; anything else (`%C %D %e %F %n %t %T %u %V %Ey %#f %#s`, a trailing `%`) makes the whole `%time{}` print nothing. The `#` flag drops leading zeros, gives long dates for `%#c`/`%#x`, and the zone name for `%#z`/`%#Z`. 255 bytes print, 256 print nothing. `%%f` prints `%f`. `%time{%Y]` with no brace prints as written.
  - `a%{onerror}b%{stderr}c` on win.ini printed `a` (stderr empty); `a%{onerror}b%{onerror}c` on a missing file printed `abc`, exit 37.
- Decision (ADR-0038): Windows dialect with fixed en-US names and layouts, because `Curl.Console` is `InvariantGlobalization` and the invariant layouts match no curl. The glibc dialect for Linux/macOS is filed as BL-289.
- Result: 87 Curl.Output tests pass; `Measure-CodeQuality.ps1 -Library Curl.Output.UnitLibrary` reports 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -w %{onerror} stops after a successful transfer and %time{format} renders as the Windows curl 8.21.0 does
