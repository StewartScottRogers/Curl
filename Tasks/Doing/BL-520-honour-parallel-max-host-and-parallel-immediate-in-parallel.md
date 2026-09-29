---
id: BL-520
title: Honour --parallel-max-host and --parallel-immediate in parallel runs
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-520 — Honour --parallel-max-host and --parallel-immediate in parallel runs

## Goal

In a `-Z` run no more than `--parallel-max-host` transfers talk to one host at once, and `--parallel-immediate` opens new connections at once rather than waiting to reuse one, as curl 8.21.0 does for HTTP/1.1 connections.

## Context

- Conformance audit 2026-09-28, row 9 (Blocker). Builds on BL-519's scheduler and BL-518's ADR.
- Curl speaks HTTP/1.1 only (ADR-0017), so multiplexing never applies; `--parallel-immediate`'s observable effect is how many connections are opened and when. Measure it with `Record-CurlExchange.ps1 -Connections 3` (count accepted connections, `%{num_connects}` and `%{conn_id}` per transfer) with and without the option, and `--parallel-max-host 1` with three URLs on one host.

## Acceptance criteria

- [ ] Measured first as above; the connection counts and `-w` values copied into Notes.
- [ ] `Curl.Console.UnitTests` tests pin the per-host limit (never more than the limit concurrent for one host, other hosts unaffected) and the measured connection behaviour of `--parallel-immediate`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
