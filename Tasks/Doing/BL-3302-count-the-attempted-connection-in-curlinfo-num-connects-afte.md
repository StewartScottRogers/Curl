---
id: BL-3302
title: Count the attempted connection in CURLINFO_NUM_CONNECTS after a connect timeout
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3302 — Count the attempted connection in CURLINFO_NUM_CONNECTS after a connect timeout

## Goal

CURLINFO_NUM_CONNECTS (0x20001A) reads 1 after a transfer whose connection attempt timed out (rc=28), as libcurl 8.22.0 does.

## Context

BL-3237's replay of e_in.json (tls-notls-server: https to a plain HTTP loopback port with CURLOPT_SSL_VERIFYPEER 0 and CURLOPT_SSL_VERIFYHOST 0 timing out after 300 s): reference NUM_CONNECTS 1, Curl's Libcurl 0. Both return rc=28 with the same error text. Where it lands in Curl.Engine.UnitLibrary is not yet known.

## Acceptance criteria

- [ ] The e_in case of LibcurlEasyDifferentialTests shows no NUM_CONNECTS difference.
- [ ] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 70f7c991 ebc469ce 0ac4089b.
