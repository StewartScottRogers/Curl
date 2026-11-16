# ADR-1283 — A libcurl RTSP RECEIVE keeps its connection, and CURLOPT_TIMEOUT bounds every library perform

- Status: Accepted
- Date: 2026-11-16
- Task: BL-5291
- Decided by Claude under Stewart's delegation.

## Context

Surl's measurement found that after `SETUP` and `PLAY`, the port's `CURL_RTSPREQ_RECEIVE` dropped the
connection (so the next perform opened a new one), reported no session ID or `CSeq` counters, and
never timed out when nothing arrived, where libcurl 8.21.0 reads on the session's connection, keeps
`CURLINFO_RTSP_SESSION_ID` and the counters, and ends `CURLE_OPERATION_TIMEDOUT` (28).

## Decision

- A libcurl handle's `RECEIVE` leaves its connection in the pool (`RtspProtocolHandler`; the curl
  tool never makes one) and reports the handle's session ID, client `CSeq` and the last reply's `CSeq`
  (`RtspSessionState.LastReceivedSequenceNumber`).
- A library handle starts the `-m` watchdog on every perform, as it already started the low-speed one,
  so `CURLOPT_TIMEOUT` ends a perform that waits with 28 (`ManagedEasyTransfer.ProgressStartingWatchdogsOf`).
  Before, only the curl tool's runner got it, and a library handle's `CURLOPT_TIMEOUT` was ignored.

## Consequences

Each `RECEIVE` still hands over what one read gives; frames that arrive together arrive in one call.
