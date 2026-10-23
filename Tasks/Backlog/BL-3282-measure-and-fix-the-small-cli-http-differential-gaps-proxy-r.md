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
completed:
---
# BL-3282 — Measure and fix the small CLI HTTP differential gaps: proxy reset text, --curves on Schannel, --local-port, --anyauth -v

## Goal

The differential cases proxy-tunnel, proxy-connect-fail, curves, local-port and auth-all-challenges-v each match curl 8.22.0, or are filed as their own task.

## Context

From BL-3233's run: proxy-tunnel and proxy-connect-fail (-sS -p -x http://127.0.0.1:18080 ...): real says "Recv failure: Connection was aborted", Curl "Connection was reset". curves (--curves nope https://127.0.0.1:18080/): Curl exits 59 "failed setting curves list", real (Schannel) goes on and times out on the plain-HTTP port. local-port (--local-port 49000-49010): real exits 7, Curl 0; check whether Curl binds the range at all. auth-all-challenges-v: verbose stderr differs past the report's truncation; rerun to see where. Widen touches when a gap lands elsewhere.

## Acceptance criteria

- [ ] Each of the five cases matches curl 8.22.0 with a focused unit test, or has its own filed task.
- [ ] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

## Log

- 2026-10-23: Created.
