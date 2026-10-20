---
id: BL-2870
title: Make upstream case 1548 pass: TELNET reading stdin with -m 1 ends with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed:
---
# BL-2870 — Make upstream case 1548 pass: TELNET reading stdin with -m 1 ends with exit 28

## Goal

Upstream case 1548 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason curl itself diverges is recorded in an ADR.

## Context

Split from BL-2831. test1548 runs `-m 1 telnet://%HOSTIP:%HTTPPORT` with `GET /we/want/1548 HTTP/1.0` + blank line on stdin, against the `http` server (sws) whose `<data>` is empty; expected exit 28. Measured 2026-10-19 on Windows: Curl exits 0. The gap office measured it `match | reference-diverges`: the reference curl also does not give 28 on the gap runner.

Likely cause to check first: upstream's sws, given an empty reply, sends nothing - check `sws.c` at curl-8_22_0's `send_doc` for whether a zero-length reply closes the connection (`!count` -> not persistent) or leaves it open so curl's `-m 1` fires. The harness's `SwsHttpServerConnection` closes on an empty part (see `Curl.Conformance.UnitLibrary/CLAUDE.md`); if sws keeps it open, that is harness work (verdict (b), add `Curl.Conformance.UnitLibrary` and its tests to `touches`). If the harness is right, measure real curl (`Record-CurlExchange.ps1` with an empty reply, local curl 8.22.0 Schannel; curl's Windows telnet reads stdin through a separate thread) and fix `Curl.Protocol.Telnet.UnitLibrary` or record (c).

## Acceptance criteria

- [ ] Notes record real curl's exit code for 1548 and a verdict (a), (b) or (c) as BL-2831 defines them.
- [ ] For (a) or (b), 1548 passes with `$env:CURL_UPSTREAM_CASE='1548'` and is on `PassingUpstreamCases.txt`; for (c) an ADR "Decided by Claude under Stewart's delegation" names the case and its reason.
- [ ] A product fix has a focused unit test in its library's `.UnitTests` project that fails without it (ADR-0482).
- [ ] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 68d4214d 1b93baff bb323509 68ad5856.
