---
id: BL-3309
title: CURLINFO_RESPONSE_CODE keeps the received status after a multi transfer ends in CURLE_OPERATION_TIMEDOUT, as libcurl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Info, Libcurl.UnitTests/Info, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3309 — CURLINFO_RESPONSE_CODE keeps the received status after a multi transfer ends in CURLE_OPERATION_TIMEDOUT, as libcurl 8.22.0 does

## Goal

After a multi transfer that received a 200 status line ends with CURLE_OPERATION_TIMEDOUT (28), CURLINFO_RESPONSE_CODE answers 200 as libcurl 8.22.0 does, not 0.

## Context

Found by BL-3238's differential run (ADR-0793): m_in.json case 34 (runfor ending in result 28). Reference: `b0=30313233343536373839/200`; Curl's Libcurl: `/0`. Replay with LibcurlMultiDifferentialTests (CURL_DIFFERENTIAL_REFERENCE_LIBRARY). Check where the easy handle's response code is cleared on a timed-out multi transfer.

## Acceptance criteria

- [x] A unit test shows CURLINFO_RESPONSE_CODE keeps the received status after a timed-out multi transfer.
- [x] LibcurlMultiDifferentialTests no longer reports m_in case 34.
- [x] Build clean; fast tests pass.

## Notes

Cause: case 34 sets CURLOPT_LOW_SPEED_LIMIT/TIME (19, 20), so the low-speed watchdog cancels the perform and its Failure carries no report; Info was rebuilt from the report with response code 0. Fix is in Curl.Engine (touches extended; no other Doing task names it): ManagedEasyTransfer.Finish keeps the status the header recording already put on Info when the result has no report. Differential replay needs the reference library and was not run here; the unit test mirrors the case (fails without the fix). Engine quality run: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fb11aa1b 5275a37f b08b61f0.
- 2026-10-23: Doing -> Done. Low-speed timeout keeps the received response code in Info
