---
id: BL-3282
title: Measure and fix the small CLI HTTP differential gaps: proxy reset text, --curves on Schannel, --local-port, --anyauth -v
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ConnectFailureReason.cs, Curl.Cli.UnitLibrary/LocalPortRange.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3282 — Measure and fix the small CLI HTTP differential gaps: proxy reset text, --curves on Schannel, --local-port, --anyauth -v

## Goal

The differential cases proxy-tunnel, proxy-connect-fail, curves, local-port and auth-all-challenges-v each match curl 8.22.0, or are filed as their own task.

## Context

From BL-3233's run: proxy-tunnel and proxy-connect-fail (-sS -p -x http://127.0.0.1:18080 ...): real says "Recv failure: Connection was aborted", Curl "Connection was reset". curves (--curves nope https://127.0.0.1:18080/): Curl exits 59 "failed setting curves list", real (Schannel) goes on and times out on the plain-HTTP port. local-port (--local-port 49000-49010): real exits 7, Curl 0; check whether Curl binds the range at all. auth-all-challenges-v: verbose stderr differs past the report's truncation; rerun to see where. Widen touches when a gap lands elsewhere.

## Acceptance criteria

- [x] Each of the five cases matches curl 8.22.0 with a focused unit test, or has its own filed task.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- 2026-10-23 lane-5 run. The full CLI HTTP differential could not run (port 18080 was held by another lane's run), so each case was measured on its own with Record-CurlExchange.ps1 against curl 8.22.0 Schannel (Git ucrt64) and Curl.Console's Debug build. All four gaps that are real are outside this task's touches, so each has its own task and no code changed here:
  - proxy-tunnel / proxy-connect-fail: a recorder that answers CONNECT with 200 Content-Length 0 and closes gives "curl: (56) Recv failure: Connection was reset" from both binaries (2 runs each). The same "aborted" (real) vs "reset" (Curl) split does reproduce in auth-all-challenges-v, on a reused connection the server had closed. Filed BL-3304.
  - curves: real Schannel with -sSk --curves nope against a TLS recorder exits 0 and prints the body; Curl exits 59 "failed setting curves list: 'nope'". ADR-0151/ADR-0284 chose to honour --curves everywhere, so changing it means superseding that choice in an ADR. Filed BL-3305.
  - local-port: run alone, both bind 49000 and exit 0. Run back to back against the same server port, whichever binary meets the 4-tuple still in TIME_WAIT exits 7 "Address already in use", so the case's exit-code difference is run-order noise. A real difference showed up on the way: Curl's failure line says "from 0.0.0.0 port 0" where real curl names the bound port ("port 49000"). Filed BL-3306.
  - auth-all-challenges-v: the verbose diff (recorder, -Connections 3) holds the aborted/reset line (BL-3304) and a missing "* Digest authentication problem, ignoring." on the second 401. Filed BL-3307.
- No library changed, so Measure-CodeQuality.ps1 had no library to measure. dotnet build: 0 warnings, 0 errors. Fast tests: 42125 passed, 0 failed.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a8d1b4cd 69edf767.
- 2026-10-23: Doing -> Done. The five CLI HTTP differential gaps are measured; local-port is run-order noise, and the four real gaps are filed as BL-3304 to BL-3307
