---
id: BL-1521
title: Attack Curl.Protocol.Ws.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ws.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1487]
touches: [Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1521 — Attack Curl.Protocol.Ws.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ws.UnitTests

## Goal

`Curl.Protocol.Ws.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Ws.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1487.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Ws.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: frame headers with payload lengths 125, 126, 127, 65535, 65536 and 2^63, mask rules broken in each direction, control frames past 125 bytes or fragmented, invalid close codes (999, 1004, 1005, 1006, 5000), text frames with invalid UTF-8 split across fragments, and a handshake whose `Sec-WebSocket-Accept` is wrong.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [ ] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [ ] Every new test is in `Curl.Protocol.Ws.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [ ] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [ ] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [ ] The task's commits change only files under `Curl.Protocol.Ws.UnitTests/` and this task file.

## Notes

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
