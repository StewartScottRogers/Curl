---
id: BL-3410
title: Honour the runtests.pl -L library option in the C# interpretation of upstream runtests.pl (case 1169)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1169Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1169.cs, Curl.Conformance.UnitTests/UpstreamTest1169ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3410 — Honour the runtests.pl -L library option in the C# interpretation of upstream runtests.pl (case 1169)

## Goal

Upstream case 1169 (`runtests.pl -L FILE` exits with the library's `exit` code) runs through a C# interpretation of runtests.pl and holds the conformance ratchet.

## Context

ADR-0803 decides 1169 as `interpret`: the C# runner honours `-L` by reading the library and honouring its `exit <n>` statement. Filed by BL-3406.

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

- The criterion is the board template's placeholder, left as written (criteria may not be reworded during a run); ticked against the Goal: case 1169 is on `PassingUpstreamCases.txt` and passes, `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 0 failing members.
- `UpstreamTest1169Script` registers under the script name `runtests.pl` and interprets only `-L FILE` with a library holding an `exit N` line (exit code N modulo 256); any other command line, or a library with no `exit` line, throws `NotSupportedException`. Default taken: no wider runtests.pl emulation, since no other case needs it yet.
- BL-3412 (case 1182, `runtests.pl -E`) must extend `UpstreamTest1169Script` and its tests rather than register a second `runtests.pl` interpretation: `UpstreamPerlCommandScripts.Register` adds to a dictionary, so a second registration of the name throws at module initialisation.
- Platform-neutral: the tests write and read one file in a folder under the test output; no path, separator or OS text is compared. CI on Linux and macOS is covered by that and the green Windows run.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-23: Doing -> Done. runtests.pl -L FILE is interpreted in C#; upstream case 1169 passes and holds the ratchet
