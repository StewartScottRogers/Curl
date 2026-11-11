---
id: BL-4942
title: Correct the nats help text on passing a token with -u
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsExtension.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4942 — Correct the nats help text on passing a token with -u

## Goal

The nats `--extended-help` entry shows the form that works, `-u token:`.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding EXT-020 (Low, confirmed: reproduced at least twice).

- Repro: NATS mock requiring token auth on 18603; in a terminal `curl -u tok "nats://127.0.0.1:18603/greet?count=1"`.
- Expected: Help says `-u` can carry "a token alone": sent without a prompt.
- Actual: Prompts `Enter host password for user 'tok':` (as curl does for `-u user`); `-u tok:` and `nats://tok@host/` work.
- Where to start: Description string in `NatsExtension.cs`.

## Acceptance criteria

- [ ] `curl --extended-help` shows the `-u token:` form for nats.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: d36bf81d a0b49a29.
