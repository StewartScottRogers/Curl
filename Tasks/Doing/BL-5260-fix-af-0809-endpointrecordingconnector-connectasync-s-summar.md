---
id: BL-5260
title: Fix AF-0809: EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs]
requirement: none
created: 2026-11-15
completed:
---
# BL-5260 — Fix AF-0809: EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both

## Goal

The defect the audit office reported as AF-0809 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0809 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:16`

Location: `Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:16`

Summary (lines 16-17): 'Connects as the wrapped connector does and records the connection it opened, or a failed connect's last socket error (BL-2374).' The success branch, lines 27-28: 'recorder.Record(connect.LocalEndPoint, connection.RemoteEndPoint, connect.IsReused); recorder.RecordFailedConnect(connect.LastSocketError);'. So on success it also records the LastSocketError of an earlier address that failed, as CURLINFO_OS_ERRNO (commit 72b7c4ac82, BL-3328). The behaviour is intended, but the summary's 'or' hides the success-path side effect.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs -Pattern 'records the connection it opened, or a failed|RecordFailedConnect\(connect.LastSocketError\);'
```

- Expected: The summary says a connect that succeeds after an earlier address failed also records that address's socket error (BL-3328).
- Actual: EndPointRecordingConnector.cs:16: /// Connects as the wrapped connector does and records the connection it opened, or a failed / EndPointRecordingConnector.cs:28: recorder.RecordFailedConnect(connect.LastSocketError); (inside the connect.Connection is { } success branch)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
