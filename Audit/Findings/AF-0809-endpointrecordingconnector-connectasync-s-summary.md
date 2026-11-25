---
id: AF-0809
title: EndPointRecordingConnector.ConnectAsync's summary says it records the connection or a failed connect's socket error; on success it records both
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs:ConnectAsync:false-doc-comment
reproduction: none
task: BL-5260
tasks: BL-5260
found: 2026-11-15
found-at: 2a5b0f025b7c8fef02cb85fb555414235eb4e132
scorecard: 2026-11-15_1610.md
duplicate-of:
closed: 2026-11-16
closed-how: reliable-reaudit
closed-by: 2026-11-16_0514.md
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

- 2026-11-16 | 2026-11-16_0514.md | reproduces: no | Ran Select-String -Path Curl.Engine.UnitLibrary/EndPointRecordingConnector.cs -Pattern 'records the connection it opened, or a failed|RecordFailedConnect\(connect.LastSocketError\);'. Only line 29, the code line 'recorder.RecordFailedConnect(connect.LastSocketError);', matches. The old summary text no longer exists. The summary at lines 16-18 now reads 'Connects as the wrapped connector does and records the connection it opened; a connect that succeeds after an earlier address failed also records that address's last socket error, and a failed connect records its last socket error and proxy error', which matches what the code does on success (lines 28-29) and on failure (line 33).

## Log

- 2026-11-15: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_0514.md: the reproduction no longer reproduces.
