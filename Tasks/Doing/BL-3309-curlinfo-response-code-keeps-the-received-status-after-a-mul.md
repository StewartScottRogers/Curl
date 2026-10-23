---
id: BL-3309
title: CURLINFO_RESPONSE_CODE keeps the received status after a multi transfer ends in CURLE_OPERATION_TIMEDOUT, as libcurl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Info, Libcurl.UnitTests/Info]
requirement: none
created: 2026-10-23
completed:
---
# BL-3309 — CURLINFO_RESPONSE_CODE keeps the received status after a multi transfer ends in CURLE_OPERATION_TIMEDOUT, as libcurl 8.22.0 does

## Goal

After a multi transfer that received a 200 status line ends with CURLE_OPERATION_TIMEDOUT (28), CURLINFO_RESPONSE_CODE answers 200 as libcurl 8.22.0 does, not 0.

## Context

Found by BL-3238's differential run (ADR-0793): m_in.json case 34 (runfor ending in result 28). Reference: `b0=30313233343536373839/200`; Curl's Libcurl: `/0`. Replay with LibcurlMultiDifferentialTests (CURL_DIFFERENTIAL_REFERENCE_LIBRARY). Check where the easy handle's response code is cleared on a timed-out multi transfer.

## Acceptance criteria

- [ ] A unit test shows CURLINFO_RESPONSE_CODE keeps the received status after a timed-out multi transfer.
- [ ] LibcurlMultiDifferentialTests no longer reports m_in case 34.
- [ ] Build clean; fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fb11aa1b 5275a37f b08b61f0.
