---
id: BL-1496
title: Attack Curl.Cookies.UnitLibrary with adversarial black-box tests in Curl.Cookies.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1462]
touches: [Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1496 — Attack Curl.Cookies.UnitLibrary with adversarial black-box tests in Curl.Cookies.UnitTests

## Goal

`Curl.Cookies.UnitTests` gains adversarial black-box tests that attack `Curl.Cookies.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1462.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Cookies.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `Set-Cookie` and cookie-jar parsing: `Domain` with a leading dot, a public suffix, an IP address or a different case; `Path` edge forms; `Expires` far past, far future, malformed and with two-digit years; `Max-Age` of 0, negative and past `long.MaxValue`; name and value at and past curl's 4096-byte limits and its per-domain count limit; Netscape jar lines with too few tabs, `#HttpOnly_` prefixes and comments; prefix rules for `__Secure-` and `__Host-`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [ ] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [ ] Every new test is in `Curl.Cookies.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [ ] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [ ] `dotnet build Curl.Cookies.UnitTests -warnaserror` is clean and `dotnet test Curl.Cookies.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [ ] The task's commits change only files under `Curl.Cookies.UnitTests/` and this task file.

## Notes

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
