---
id: BL-1619
title: Make Curl.Protocol.Http.UnitTests' HttpRangeHeaderTests to HttpRequestHeadFormatterTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1619 — Make Curl.Protocol.Http.UnitTests' HttpRangeHeaderTests to HttpRequestHeadFormatterTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Protocol.Http.UnitTests` files (8 files, 128 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `HttpRangeHeaderTests.cs`, `HttpRedirectLocationTests.cs`, `HttpRequestBodyWriterTests.cs`, `HttpRequestFramingTests.cs`, `HttpRequestHeadFormatterTests.AltSvc.cs`, `HttpRequestHeadFormatterTests.H2cUpgrade.cs`, `HttpRequestHeadFormatterTests.TrEncoding.cs`, `HttpRequestHeadFormatterTests.cs`.

## Context

- Split from BL-1476 (one task per range of files, as its Notes direct); BL-1476 keeps the whole-project checks and depends on this task. Follow BL-1476's Context: what matters in this project (request line and headers, the scripted response, the parsed status, headers and body, each redirect or authentication step, the `CurlExitCode` with its error text), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1478 (`Curl.Protocol.Ldap.UnitTests`) shows the pattern.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Protocol.Http.<Class>.` term per class the listed files declare (the partial `HttpRequestHeadFormatterTests`'s four files count once), joined with `|`.

## Acceptance criteria

- [ ] `dotnet test Curl.Protocol.Http.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [ ] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [ ] `dotnet build Curl.Protocol.Http.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Http.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Protocol.Http.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 2 could not integrate: fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then Curl.Cookies.UnitTests failed) after rebasing onto the other lanes' work. The work is on branch factory/BL-1619-lane-2-20261007-111121; start with git cherry-pick --no-commit factory/BL-1619-lane-2-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
