---
id: AF-0809
title: EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both
auditor: truthfulness
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:ConnectAsync:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0809 - EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both

## Summary

Low finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:16`: EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both.

## Evidence

Location: `Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:16`

Summary (lines 16-17): 'Connects as the wrapped connector does and records the connection it opened, or a failed connect's last socket error (BL-2374).' The success branch, lines 27-28: 'recorder.Record(connect.LocalEndPoint, connection.RemoteEndPoint, connect.IsReused); recorder.RecordFailedConnect(connect.LastSocketError);'. So on success it also records the LastSocketError of an earlier address that failed, as CURLINFO_OS_ERRNO (commit 72b7c4ac82, BL-3328). The behaviour is intended, but the summary's 'or' hides the success-path side effect.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs -Pattern 'records the connection it opened, or a failed|RecordFailedConnect\(connect.LastSocketError\);'
```

- Expected: The summary says a connect that succeeds after an earlier address failed also records that address's socket error (BL-3328).
- Actual: EndPointRecordingConnector.cs:16: /// Connects as the wrapped connector does and records the connection it opened, or a failed / EndPointRecordingConnector.cs:28: recorder.RecordFailedConnect(connect.LastSocketError); (inside the connect.Connection is { } success branch)

## Re-audits

## Log

- 2026-11-15: filed proposed.
