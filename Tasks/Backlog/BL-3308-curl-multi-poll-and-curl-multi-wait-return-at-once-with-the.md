---
id: BL-3308
title: curl_multi_poll and curl_multi_wait return at once with the busy transfer's socket ready, as libcurl 8.22.0 does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Multi/Wait, Libcurl.UnitTests/Multi]
requirement: none
created: 2026-10-23
completed:
---
# BL-3308 — curl_multi_poll and curl_multi_wait return at once with the busy transfer's socket ready, as libcurl 8.22.0 does

## Goal

curl_multi_poll and curl_multi_wait, called with no extra sockets while a transfer is waiting on a slow body, return at once with numfds 1 as libcurl 8.22.0 does, instead of sleeping the whole timeout with 0.

## Context

Found by BL-3238's differential run (ADR-0793): m_in.json case 24 (/slow, add, perform, pollms 800 twice, waitms 800). Reference: `poll=0/1/0 poll=0/1/0 wait=0/1/0`; Curl's Libcurl: `poll=0/0/4 poll=0/0/4 wait=0/0/4` (ready count / elapsed in 200 ms buckets). Replay with LibcurlMultiDifferentialTests (CURL_DIFFERENTIAL_REFERENCE_LIBRARY). Start in Libcurl/Multi/Wait/CurlMulti.Wait.cs and the engine multi transfer's wait.

## Acceptance criteria

- [ ] A unit test in Libcurl.UnitTests shows poll and wait return with numfds 1 before the timeout while the transfer's socket is waited on.
- [ ] LibcurlMultiDifferentialTests no longer reports m_in case 24's poll and wait tokens.
- [ ] Build clean; fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
