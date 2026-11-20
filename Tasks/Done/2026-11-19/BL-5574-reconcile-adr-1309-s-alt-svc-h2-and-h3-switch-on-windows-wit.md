---
id: BL-5574
title: Reconcile ADR-1309's alt-svc h2 and h3 switch on Windows with the Schannel build ADR-1238 measured
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs, Documentation/Planning/Decisions/ADR-1309-a-stored-alt-svc-h3-entry-switches-a-transfer-to-http3-on-every-platform.md, Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs, Documentation/Planning/Decisions/ADR-1313-alt-svc-with-no-version-option-stays-on-the-origin-for-h2-and-h3-on-windows.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5574 — Reconcile ADR-1309's alt-svc h2 and h3 switch on Windows with the Schannel build ADR-1238 measured

## Goal

With no version option, Curl's alt-svc behaviour on Windows matches what the Windows reference curl (8.22.0 Schannel) measurably does, and a test pins each platform's answer.

## Context

Found by the shift-end review of 2026-11-19 (code-reviewer) in the diff 74adc46d10...29c53fa22a (BL-5549, ADR-1309).

- **What changed:** `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs` `LookupOf` (around line 257) now lets a transfer with no version option switch to `h1`, `h2` and `h3` on every platform. The diff removed the `isWindows` switch that ADR-1238 added and deleted the tests that pinned the Windows answer: `AltSvcTransferCacheNoQuicTests`, and the Windows case in `CurlCommandRunnerAltSvcVersionTests`.
- **Why that is a regression:**
  - ADR-1238 rests on a measurement. The outside fitness review's findings STD-009 and STD-013, each reproduced at least twice, showed the Schannel curl staying on the origin for both an `h2` entry and an `h3` entry.
  - ADR-1309 says it was "Not measured against a live HTTP/3-capable curl". It justifies the change by Curl's own `curl -V` (ADR-1282) rather than by real curl's behaviour.
  - CLAUDE.md's standing rules say to match the platform's curl (the Schannel build on Windows), to measure real curl before pinning, and, where curl's answer differs by platform, to pin each platform in its own test.
- **Failure:** on Windows, `curl --alt-svc f https://host/` with a stored `h2="alt:443"` entry now connects to `alt`, while real Windows curl stays on the origin. This reopens STD-009 and STD-013.

## Acceptance criteria

- [x] Real Windows curl 8.22.0 is measured with `Record-CurlExchange.ps1` (or the measurement STD-009 and STD-013 recorded is cited) for no version option with an `h2` entry and with an `h3` entry. The result is written in an ADR that amends or supersedes ADR-1309 and is "Decided by Claude under Stewart's delegation".
- [x] `AltSvcTransferCache.LookupOf` gives the measured Windows answer. Either it gives that answer, or the ADR explains why Curl deliberately departs from the Schannel build, given that `curl -V` now lists HTTP2 and HTTP3, and records that the outside review's findings are superseded.
- [x] `Curl.Engine.UnitTests` pins the Windows answer in a test marked `[OSCondition(OperatingSystems.Windows)]` and the other platforms' answer in one marked `[OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]`.
- [x] `AltSvcTransferCache`'s doc comments and ADR-1309's status line say what the code does.
- [x] `dotnet build` is clean, the fast tests pass, and coverage stays at 100%.

## Notes

- Measured: the Windows reference curl 8.22.0 Schannel `-V` lists neither HTTP2 nor HTTP3; with ADR-1238 (STD-009, STD-013 reproduced twice) that means h1 alone with no version option. Decided in ADR-1313: Windows restored to h1 alone, other platforms keep ADR-1309. Tests pin both under OSCondition (AltSvcTransferCacheH3SwitchTests). Engine fast tests pass, Measure-CodeQuality clean for Curl.Engine.UnitLibrary (LookupOf complexity kept at 10 by an extracted helper). Added the new test file and ADR to touches.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 2b810262 a6d6a214 0266d876 dff2d5bc d559aadd.
- 2026-11-19: Doing -> Done. Windows alt-svc with no version option stays on h1 again, per measured Schannel curl; each platform pinned (ADR-1313)
