---
id: BL-3441
title: Rename UpstreamTest1169Script to UpstreamRunTestsScript now that it also runs case 1182's -E, and fix its Run doc and number parsing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs, Curl.Conformance.UnitLibrary/UpstreamRunTestsScript.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1169.cs, Curl.Conformance.UnitTests/UpstreamTest1169ScriptTests.cs, Curl.Conformance.UnitTests/UpstreamRunTestsScriptTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3441 — Rename UpstreamTest1169Script to UpstreamRunTestsScript now that it also runs case 1182's -E, and fix its Run doc and number parsing

## Goal

The class that interprets upstream's `runtests.pl` is named for that script rather than for one case, every member's doc comment says what the member does, and every command line it does not interpret throws `NotSupportedException` as its remarks promise.

## Context

Found by the shift-end review of 2026-10-23 (ADR-0710, type-design-analyzer and silent-failure-hunter) in the shift merged at `bb39553be`.

1. **Name.** BL-3438 extended `Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs:21` to interpret case 1182's `runtests.pl [-o NAME=VALUE]... -E FILE TEST...` as well as case 1169's `-L FILE`. The class's own summary now names both cases, so `UpstreamTest1169Script` no longer says what it does (root `CLAUDE.md`, "Say what it does, do what it says"). Someone looking for where `runtests.pl -E` is emulated will not find it by name. Rename the class to `UpstreamRunTestsScript` (it already has `ScriptName = "runtests.pl"`), its file to match, and its test class and file `UpstreamTest1169ScriptTests` to `UpstreamRunTestsScriptTests`. Update the registration in `Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1169.cs` (that registration file may keep its name if it is the 1169 entry of a per-case registration list; say which in the Log).
2. **Stale doc.** The one-argument `public static int Run(IReadOnlyList<string> arguments)` is still documented as "Carries out a `runtests.pl` command line of the form `-L FILE`" returning "the exit code of the library's first `exit` line". It now also accepts `-E ...`, returns 1, and throws away the `test NNNN SKIPPED: reason` lines. Either restrict it to `-L FILE` (throwing `NotSupportedException` otherwise) or document what it does now.
3. **Wrong exception.** `ParseTestNumber` uses `int.Parse` on each test word after `-E FILE` and on each pattern in an exclude file's `test:` line, so a non-numeric one (a keyword or glob pattern) throws `FormatException`, not the `NotSupportedException` the class's remarks promise for anything it does not interpret. Use `int.TryParse` and throw `NotSupportedException` naming the word.

## Acceptance criteria

- [ ] No `.cs` file in the solution names `UpstreamTest1169Script`; `Curl.Conformance.UnitLibrary/UpstreamRunTestsScript.cs` holds `internal static class UpstreamRunTestsScript`, and its tests are in `Curl.Conformance.UnitTests/UpstreamRunTestsScriptTests.cs`.
- [ ] The one-argument `Run` overload's doc comment states exactly which command lines it accepts and what it returns, and a unit test pins that behaviour.
- [ ] A unit test pins that `-E FILE abc` throws `NotSupportedException`, and another that an exclude file line `test:abc:reason` makes the run throw `NotSupportedException`.
- [ ] The upstream cases 1169 and 1182 still pass (their rows in `PassingUpstreamCases.txt` stay).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports the renamed class inside every gate.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 7151d7a3 d69b8d66 5eb137c4 1a291900 cb47ba9b 779790c0.
