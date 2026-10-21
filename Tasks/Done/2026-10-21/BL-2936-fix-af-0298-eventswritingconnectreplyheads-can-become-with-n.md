---
id: BL-2936
title: Fix AF-0298: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests/TransferContextFactoryConnectReplyHeadTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2936 — Fix AF-0298: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D

## Goal

The defect the audit office reported as AF-0298 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0298 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0298-eventswritingconnectreplyheads-can-become-with-no.md`.

Location: `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`

Location: `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`

Sampled mutant (seed 0) survived: IsHttpOrHttpsProxy(proxy) && headerOutput is not null && !proxyOptions.SuppressConnectHeaders => IsHttpOrHttpsProxy(proxy) && headerOutput is not null || !proxyOptions.SuppressConnectHeaders. With the default SuppressConnectHeaders = false, the condition is now always true. So a transfer through an HTTP(S) proxy tunnel with no header output (no -i, -I or -D, the common case) gets ConnectReplyHeadWritingEvents(events, null), whose WriteConnectReplyHeadAsync calls headerOutput.WriteAsync (ConnectReplyHeadWritingEvents.cs:26): a NullReferenceException on the CONNECT reply, so the exit code and output change. No test runs the context factory over a proxy tunnel without header output.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferContextFactory.cs:592:&& -Member EventsWritingConnectReplyHeads -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right; the gap was the test. Added
  `Curl.Engine.UnitTests/TransferContextFactoryConnectReplyHeadTests.cs`, which builds a context
  through `TransferContextFactory.Create` with an HTTP proxy and checks whether `Events` is an
  `IConnectReplyHeadWritingEvents`: wrapped with `-D` output; not wrapped with no header output
  (the case that kills AF-0298's mutant), under `--suppress-connect-headers`, or through a SOCKS5 proxy.
- `touches` widened to the new test file: the test belongs in the library's own `.UnitTests`, and no
  other task in Doing on `origin/work/dark-factory` names `Curl.Engine.UnitTests`.
- The audit guard does not let a lane run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was
  applied by hand instead (`&&` before `!proxyOptions.SuppressConnectHeaders` changed to `||`, line
  592). Under warnings-as-errors that mutant does not compile (CS8604, null `headerOutput`), so it was
  built with `-p:TreatWarningsAsErrors=false`: 3 of the 4 new tests fail on it (killed), and all 4 pass
  once it is reverted. The quality auditor's re-audit still has to confirm it with the tool.
- No production file changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. A context-factory test now kills AF-0298's && -> || mutant: an HTTP proxy with no header output keeps the plain events
