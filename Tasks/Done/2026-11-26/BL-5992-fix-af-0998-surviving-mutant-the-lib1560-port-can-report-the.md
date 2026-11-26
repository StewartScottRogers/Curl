---
id: BL-5992
title: Fix AF-0998: Surviving mutant: the lib1560 port can report the zoneid part as UNKNOWN (> -> >=)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs, Curl.Conformance.LibTest.UnitTests/Lib1560ChecksTests.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5992 — Fix AF-0998: Surviving mutant: the lib1560 port can report the zoneid part as UNKNOWN (> -> >=)

## Goal

The defect the audit office reported as AF-0998 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0998 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs:134`

Mutant 'if (part > CurlUrlPart.ZoneId)' -> 'if (part >= CurlUrlPart.ZoneId)' survived. Lib1560Tables.SetParts.cs:40 sets 'zoneid=NULL,' on https://[::1%25fake]:1234/, so with the mutant the port writes a spurious "UNKNOWN part 'zoneid'" line that upstream lib1560 never writes. No test checks the port's error output for a zoneid set.

Reproduction: the audit office's mutation test changed `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs line 134` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Conformance.LibTest.UnitLibrary/Lib1560Checks.cs line 134`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Lib1560ChecksTests.cs to touches: the test that kills the mutant belongs there; no task in Doing on origin/work/dark-factory names it.
- No production change: the code was right and only the test was missing, so the feature pipeline stages were not delegated and no quality run was needed.
- Killing test: Lib1560ChecksTests.UpdateUrl_ZoneIdTheLastKnownPart_SetsItWithoutAnUnknownPartLine. With > changed to >= it was the one failure of 1244 in Curl.Conformance.LibTest.UnitTests; reverted, dotnet build had 0 warnings and 0 errors and the fast tests all passed.
- The test runs against the fake engine and compares only managed strings, so it is platform-neutral.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: f59c9e85 9742157d.
- 2026-11-25: Doing -> Done. A fast test now fails if the lib1560 port reports the zoneid part as UNKNOWN
