---
id: BL-1498
title: Attack Curl.Cryptography.UnitLibrary with adversarial black-box tests in Curl.Cryptography.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1464, BL-1525]
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-06
completed:
---
# BL-1498 — Attack Curl.Cryptography.UnitLibrary with adversarial black-box tests in Curl.Cryptography.UnitTests

## Goal

`Curl.Cryptography.UnitTests` gains adversarial black-box tests that attack `Curl.Cryptography.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1464.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Cryptography.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: the hand-built primitives as black boxes: known-answer vectors at lengths 0, 1, block-1, block and block+1; all-zero and all-0xFF keys; wrong key, nonce and tag lengths; a single flipped bit in every byte of a tag or ciphertext; Ed25519 non-canonical encodings and small-order points; X25519 low-order public keys; big-integer edge values (0, 1, p-1, p). Coordinate with the audit office's fuzzing (`audit-security`) rather than repeat it.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [ ] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [ ] Every new test is in `Curl.Cryptography.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [ ] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [ ] `dotnet build Curl.Cryptography.UnitTests -warnaserror` is clean and `dotnet test Curl.Cryptography.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [ ] The task's commits change only files under `Curl.Cryptography.UnitTests/` and this task file.

## Notes

## Log

- 2026-10-06: Created.
- 2026-10-06: Now depends on BL-1525, which speeds up X25519, X448 and CAST-128, so the attacks run against the final code.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 3 could not integrate: fast tests failed twice (Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother; then no test named) after rebasing onto the other lanes' work. The work is on branch factory/BL-1498-lane-3-20261007-111121; start with git cherry-pick --no-commit factory/BL-1498-lane-3-20261007-111121 and fix it.
