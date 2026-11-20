---
id: BL-5593
title: Make Invoke-MutationTest stop a timed-out mutant run for certain, so a failed taskkill never hangs or crashes a quality audit
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Invoke-MutationTest.ps1]
lane: no
requirement: none
created: 2026-11-20
completed:
---
# BL-5593 — Make Invoke-MutationTest stop a timed-out mutant run for certain, so a failed taskkill never hangs or crashes a quality audit

## Goal

When a mutant's test run passes `-TimeoutSeconds`, `Audit/Tools/Invoke-MutationTest.ps1` always stops it, records the mutant as `timedOut` and goes on to the next one: a `taskkill` that fails or does not take neither crashes the batch nor hangs it.

## Context

Two quality audits in a row lost time to `Stop-ProcessTree` (`Audit/Tools/Invoke-MutationTest.ps1:336`), which runs `taskkill /PID <id> /T /F 2>&1 | Out-Null` on Windows and then `$Process.WaitForExit()` with no limit:

- Audit 20261119-163306: the `Curl.Engine.UnitLibrary` batch died on `taskkill`'s stderr turned into a `NativeCommandError` ("ERROR: The process ... cannot be terminated"), so no `mutation-Curl.Engine.UnitLibrary.json` was written. The quality auditor waited in an unbounded loop for that file until it restarted the batch about an hour later (scratch/quality/mutation-Curl.Engine.UnitLibrary.log).
- Audit 20261120-045659: a `Curl.Extended.Protocol.Amqp1.UnitLibrary` mutant's `dotnet test` (testhost started 07:22, idle CPU) ran past its 600-second limit and was still running at 08:00; the batch never moved on until an interactive session killed the process tree at 08:01, after which the batch finished at once (score 0.8947).

**Fix sketch:** in `Stop-ProcessTree`, run `taskkill` so its failure is not an error record (`$ErrorActionPreference = 'Continue'` inside, or `Start-Process taskkill -Wait`); then wait a bounded time (`$Process.WaitForExit(30000)`); if the tree is still alive, fall back to `$Process.Kill($true)` (PowerShell 7) or `Stop-Process -Force` on every process in the tree (`Get-ProcessTreePeakBytes` already walks it), and never wait without a limit. Also see whether `Invoke-Timed`'s `$p.WaitForExit($Seconds * 1000)` returns on time when the child `dotnet test` has grandchildren holding the redirected output files. Keep the behaviour off Windows the same.

This is an audit path (ADR-0267): it changes only through the `audit` branch, by an interactive session.

## Acceptance criteria

- [ ] A test (in `Audit/Tools` self-tests or a `-SelfTest` switch of the script, as the other audit tools do) runs `Invoke-Timed` on a command that ignores its timeout (for example `powershell -Command Start-Sleep 600` with a 2-second limit) and gets `TimedOut = $true` back within 60 seconds.
- [ ] A test where the kill step fails (a stand-in for `taskkill` that writes to stderr and exits non-zero) still returns `TimedOut = $true` without throwing and without waiting without limit.
- [ ] `Stop-ProcessTree` never calls `WaitForExit()` without a time limit.
- [ ] The audit guard and the audit self-tests in CI (`Audit self-tests` job) pass on Windows, Linux and macOS.

## Notes

## Log

- 2026-11-20: Created from audits 20261119-163306 and 20261120-045659, where a mutant run that outlived its timeout crashed or hung the quality auditor.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 8f9cdfac 8f64067d 794478e6 55152244.
