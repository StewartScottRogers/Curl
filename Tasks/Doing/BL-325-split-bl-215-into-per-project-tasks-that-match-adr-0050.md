---
id: BL-325
title: Split BL-215 into per-project tasks that match ADR-0050
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-164]
touches: [Tasks]
requirement: none
created: 2026-09-27
completed:
---
# BL-325 — Split BL-215 into per-project tasks that match ADR-0050

## Goal

The connection-reuse work of ADR-0050 is filed as one task per project, and BL-215 covers only `Curl.Networking`.

## Context

- ADR-0050 (BL-164) needs changes in `Curl.Protocol.Abstractions.UnitLibrary` (`IConnection.MarkReusable`, `ConnectTarget.PoolScheme`, `ConnectResult.IsReused`/`ConnectionNumber`, the ADR-0046 event fields), `Curl.Protocol.Http.UnitLibrary` (calling `MarkReusable`, reuse-aware `-v` lines and `num_connects`) and `Curl.Console` (one pooling connector per command line), but BL-215 touches only `Curl.Networking.*`.
- Use `task-planner`; the contract task lands first and the others depend on it.

## Acceptance criteria

- [ ] Tasks exist for the Abstractions, Http and Console changes ADR-0050 names, each with exact `touches` and dependencies in contract-first order.
- [ ] BL-215 depends on the Abstractions task and its `touches` still names only `Curl.Networking.UnitLibrary` and `Curl.Networking.UnitTests`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
