---
id: BL-5678
title: Fix AF-0928: HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5678 — Fix AF-0928: HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists

## Goal

The defect the audit office reported as AF-0928 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0928 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:1517`

Location: `Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:1517`

The only check of the connection is Assert.IsNotNull(connected.Connection, connected.ErrorMessage); the other assertions check the target's host, port and TLS flag. The name says the connection reaches the SWS emulation, but nothing reads the scripted 'HTTP/1.1 200 OK' reply through it, so a connector that returned any other connection would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs -Pattern 'Assert.IsNotNull\(connected.Connection'
```

- Expected: No match: the test reads the SWS reply through the connection.
- Actual: UpstreamCaseRunnerTests.cs:1517:        Assert.IsNotNull(connected.Connection, connected.ErrorMessage);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
