---
id: BL-2129
title: Gap cross-check: normalise elapsed times in stderr and keep connections alive as sws does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Gap/Tools]
lane: no
requirement: none
created: 2026-10-13
completed:
---
# BL-2129 — Gap cross-check: normalise elapsed times in stderr and keep connections alive as sws does

## Goal

The gap office's reference cross-check stops reporting test1293 and test1075 as gaps when Curl and real curl behave alike: elapsed times are normalised in stderr, and the loopback server keeps connections alive as upstream's sws does.

## Context

Diagnosis on 2026-10-13 of why GF-0001 and GF-0005 stayed open (gap run 2026-10-12_2042). Both pass in process; only `Gap/Tools/Measure-ReferenceCrossCheck.ps1` turns them into gaps:

- test1293 (GF-0001): stderr differs only in `Failed to connect to 0.0.0.0:80 after 0 ms` against `after 16 ms`. `Read-Recording` (around line 181, stderr at 192) does not normalise elapsed times; the JIT Release candidate's first connect costs about 20 ms.
- test1075 (GF-0005): `Record-CurlExchange.ps1` (around line 896) closes every connection after one response, and the cross-check's recorder call (around line 255) passes no `-HoldOpenMilliseconds` or `-AnswerHeldRequests`, unlike sws, which keeps the keep-alive 401 connection open. Real curl and Curl then race the server's close differently. With `-HoldOpenMilliseconds 1500 -AnswerHeldRequests 5` the reference, JIT and AOT builds all agree.

Gap paths are interactive only; lands through a pull request from a branch built from master, merged once CI is green on all three platforms.

## Acceptance criteria

- [ ] `Read-Recording` replaces `after <n> ms` and `after <n> milliseconds` in stderr with a fixed token before comparing, like the ephemeral port masking.
- [ ] The cross-check's recordings keep a connection alive after a response unless the response says `Connection: close` or the case uses `swsclose`, using `Record-CurlExchange.ps1`'s HoldOpen and AnswerHeld parameters.
- [ ] The script's self-test has a fixture for each rule and passes.
- [ ] Re-running the cross-check on test1293 and test1075 against a current Release build gives agreement (no "stderr differs").

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing. interactive session
