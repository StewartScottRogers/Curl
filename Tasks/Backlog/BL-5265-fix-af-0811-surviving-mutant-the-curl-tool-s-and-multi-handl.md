---
id: BL-5265
title: Fix AF-0811: Surviving mutant: the curl tool's and multi handles' transfers can take ownership of their connections and close them at handle cleanup
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5265 — Fix AF-0811: Surviving mutant: the curl tool's and multi handles' transfers can take ownership of their connections and close them at handle cleanup

## Goal

The defect the audit office reported as AF-0811 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0811 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337`

The mutant changes `ToolSettings is null && !shared.MultiOwners.ContainsKey(this)` to `||` and survived the Curl.Engine.UnitTests run (seed 213433). With it, every curl-tool transfer and every multi-owned easy handle makes its opened connections its own, so they are closed when the handle is disposed. Connection reuse across a tool run's URLs ('Re-using existing connection'), FTP QUIT timing and the request bytes on later transfers would change, and no test notices.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs line 337` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs line 337`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-16: Created.
