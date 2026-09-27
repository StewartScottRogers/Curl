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
completed: 2026-09-27
---
# BL-325 — Split BL-215 into per-project tasks that match ADR-0050

## Goal

The connection-reuse work of ADR-0050 is filed as one task per project, and BL-215 covers only `Curl.Networking`.

## Context

- ADR-0050 (BL-164) needs changes in `Curl.Protocol.Abstractions.UnitLibrary` (`IConnection.MarkReusable`, `ConnectTarget.PoolScheme`, `ConnectResult.IsReused`/`ConnectionNumber`, the ADR-0046 event fields), `Curl.Protocol.Http.UnitLibrary` (calling `MarkReusable`, reuse-aware `-v` lines and `num_connects`) and `Curl.Console` (one pooling connector per command line), but BL-215 touches only `Curl.Networking.*`.
- Use `task-planner`; the contract task lands first and the others depend on it.

## Acceptance criteria

- [x] Tasks exist for the Abstractions, Http and Console changes ADR-0050 names, each with exact `touches` and dependencies in contract-first order.
- [x] BL-215 depends on the Abstractions task and its `touches` still names only `Curl.Networking.UnitLibrary` and `Curl.Networking.UnitTests`.

## Notes

- Filed BL-335 (`Curl.Protocol.Abstractions`: `MarkReusable`, `PoolScheme`, `IsReused`, `ConnectionNumber`, `ConnectionReusedEvent.Scheme`/`IsProxy`; depends on BL-164 and BL-313, which creates `ConnectionReusedEvent`), BL-336 (`Curl.Protocol.Http`; depends on BL-335, BL-173) and BL-334 (`Curl.Console`; depends on BL-335, BL-215). BL-215 now depends on BL-335 and keeps its `Curl.Networking`-only `touches`.
- Choice: filed the tasks in-session with the board script instead of through `task-planner`, since the ADR's "Who does what" already fixes the split; the result is the same set of board files.
- Formatting the reuse line from `ConnectionReusedEvent` stays with BL-228 (`Curl.Output`), as ADR-0050 says; no Output task was filed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ADR-0050 work is filed per project: BL-335 contract first, then BL-215, BL-336 and BL-334
