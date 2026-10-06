---
id: BL-1505
title: Attack Curl.Protocol.Abstractions.UnitLibrary with adversarial black-box tests in Curl.Protocol.Abstractions.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1471]
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1505 — Attack Curl.Protocol.Abstractions.UnitLibrary with adversarial black-box tests in Curl.Protocol.Abstractions.UnitTests

## Goal

`Curl.Protocol.Abstractions.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Abstractions.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1471.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Abstractions.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `CurlUrlParser` and its pieces (user info with `@` and `:`, bracketed IPv6 with zone IDs, ports 0, 65535 and 65536, percent-encoding including `%00` and bad hex, IDN, backslashes, dot segments, URLs past 8000 bytes, scheme case), `CurlUrlIPv4Address` short, octal and hex forms, `CurlDateParser` (every date form curl accepts, years 1601, 2038, 9999 and past), Alt-Svc header parsing, and the `IConnection` contract under zero-byte reads, partial reads and cancellation.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [ ] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [ ] Every new test is in `Curl.Protocol.Abstractions.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [ ] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [ ] The task's commits change only files under `Curl.Protocol.Abstractions.UnitTests/` and this task file.

## Notes

## Log

- 2026-10-06: Created.
