---
id: BL-1052
title: Print the reused connection's number as %{conn_id} for a transfer on a reused connection
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-754]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1052 — Print the reused connection's number as %{conn_id} for a transfer on a reused connection

## Goal

A transfer that reuses a pooled connection prints that connection's number as `%{conn_id}` (and in `--trace-ids`), as curl 8.21.0 does, instead of the next number.

## Context

- Measured in BL-754 Notes: `-w '[%{num_connects} %{conn_id}]' http://127.0.0.1:P/a --next -w ... http://127.0.0.1:P/b` on a kept-alive connection prints `[1 0]` then `[0 0]` from curl 8.21.0; Curl prints `[1 0]` then `[0 1]`. The same happens for two URLs in one group.
- `CurlCommandRunner.TakeConnectionId` and `EventsFor(..., () => state.ConnectionId ??= nextConnectionId++)` number every transfer that connected from the runner's own counter (BL-284 measured only closed connections). The pool's `ConnectResult.ConnectionNumber` and `ConnectionReusedEvent.ConnectionNumber` already carry the real number (ADR-0109, ADR-0285).
- BL-799's retry rule (a retried attempt that opens no connection keeps its `conn_id`) must keep passing.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test pins `[1 0][0 0]` for two URLs on one kept-alive connection, in one group and across `--next` groups, and `[1 0][1 1]` when the server closes between them.
- [x] `--trace-ids` lines of the second transfer carry `[1-0]`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- 2026-10-01 (lane 3): `ConnectionIdRecordingTransferEvents` (new, Curl.Console) wraps a transfer's events when `-w` or `--trace-ids` prints `%{conn_id}`. A connection the transfer opens (`ReportConnectionOpened`, not a second connection) takes the runner's next `%{conn_id}` and is remembered under the pool's number; `ReportConnectionReused` gives the transfer the remembered `%{conn_id}`, else the pool's number itself (curl's `#N`, ADR-0109) - the fallback covers a connection opened by a group without `-w`, and test connectors that report no opened event. The reuse line is the transfer's first event, so `--trace-ids` marks it and everything after `[1-0]`.
- Choice: wrap only when `-w` or `--trace-ids` is in the group, so a run with neither still hands the handler `NoTransferEvents.Instance` (pinned by `RunAsync_NoTraceOption_GivesTheHandlerTheSinkThatDoesNothing`). BL-799's retry tests pass unchanged.
- Tests: four in `CurlCommandRunnerOptionGroupConnectionReuseTests` ([1 0][0 0] in one group and across `--next`, [1 0][1 1] with `Connection: close`, `[1-0]` trace-ids lines) and `ConnectionIdRecordingTransferEventsTests` for every branch of the wrapper.
## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A transfer on a reused connection prints that connection's %{conn_id} and --trace-ids marker
