---
id: BL-2870
title: Make upstream case 1548 pass: TELNET reading stdin with -m 1 ends with exit 28
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2880]
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2870 — Make upstream case 1548 pass: TELNET reading stdin with -m 1 ends with exit 28

## Goal

Upstream case 1548 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason curl itself diverges is recorded in an ADR.

## Context

Split from BL-2831. test1548 runs `-m 1 telnet://%HOSTIP:%HTTPPORT` with `GET /we/want/1548 HTTP/1.0` + blank line on stdin, against the `http` server (sws) whose `<data>` is empty; expected exit 28. Measured 2026-10-19 on Windows: Curl exits 0. The gap office measured it `match | reference-diverges`: the reference curl also does not give 28 on the gap runner.

Likely cause to check first: upstream's sws, given an empty reply, sends nothing - check `sws.c` at curl-8_22_0's `send_doc` for whether a zero-length reply closes the connection (`!count` -> not persistent) or leaves it open so curl's `-m 1` fires. The harness's `SwsHttpServerConnection` closes on an empty part (see `Curl.Conformance.UnitLibrary/CLAUDE.md`); if sws keeps it open, that is harness work (verdict (b), add `Curl.Conformance.UnitLibrary` and its tests to `touches`). If the harness is right, measure real curl (`Record-CurlExchange.ps1` with an empty reply, local curl 8.22.0 Schannel; curl's Windows telnet reads stdin through a separate thread) and fix `Curl.Protocol.Telnet.UnitLibrary` or record (c).

## Acceptance criteria

- [x] Notes record real curl's exit code for 1548 and a verdict (a), (b) or (c) as BL-2831 defines them.
- [x] For (a) or (b), 1548 passes with `$env:CURL_UPSTREAM_CASE='1548'` and is on `PassingUpstreamCases.txt`; for (c) an ADR "Decided by Claude under Stewart's delegation" names the case and its reason.
- [x] A product fix has a focused unit test in its library's `.UnitTests` project that fails without it (ADR-0482).
- [x] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-19 (lane 4): real curl 8.22.0 (Schannel, `Record-CurlExchange.ps1 -Response '' -RequestReadMilliseconds 3000 -StandardInput 'GET /we/want/1548 HTTP/1.0\n\n' -CurlArgs '-m','1','telnet://127.0.0.1:18548'`) exits **28** after 1300 ms; it sends the 28 stdin bytes as they are (line feeds only, no telnet negotiation). Verdict **(b)**, harness wrong: `<data>` is one line feed, not empty, and sws never sees `\r\n\r\n` in an LF-only request, so it keeps reading and curl's `-m 1` fires; the `sws` emulation instead returns 0 from a read with no reply waiting, so Curl sees a close and exits 0. Curl's telnet handler is right; no Telnet change.
- The fix (`ReadWithNothingSentAsync` waits for the next write whenever unserved request bytes remain) makes 1548 pass, but about 15 harness tests assert the old close-on-partial-request and hang, and the whole project needs a hang-checked run and a quality run: more than this run's remaining time and cost. Filed as BL-2880 (owning the harness files, the listing and the focused test) and added to `depends-on`; the attempted fix is left uncommitted for the shift's stash. BL-2880's ID was picked above the shared board's highest (BL-2874) because this lane's stale board made the script hand out BL-2871, which BL-2871 (SFTP) already holds. Once BL-2880 is Done, this task only checks its criteria.

- 2026-10-21 (lane 3): BL-2880 (Done, 20f7ce72) made the sws emulation keep reading an unfinished request and put 1548 on `PassingUpstreamCases.txt`, carrying over the stashed fix. Verdict stays **(b)**; real curl exits 28 (recorded above). Curl.Conformance.UnitTests run with `CURL_UPSTREAM_CASE=1548`: 3650 passed, 0 failed. No Telnet change, so the focused unit test is BL-2880's in `SwsHttpServerConnectorTests` and Telnet coverage is unchanged. The stash 35c881ec is superseded and was not applied.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 68d4214d 1b93baff bb323509 68ad5856.
- 2026-10-19: Doing -> Backlog. Waits on BL-2880: the sws emulation must keep reading an unfinished request (verdict b; real curl exits 28)
- 2026-10-21: Backlog -> Doing. Criteria at claim: 68d4214d 1b93baff bb323509 68ad5856.
- 2026-10-21: Doing -> Done. 1548 passes on the ratchet via BL-2880's sws fix; verdict (b), real curl exits 28
