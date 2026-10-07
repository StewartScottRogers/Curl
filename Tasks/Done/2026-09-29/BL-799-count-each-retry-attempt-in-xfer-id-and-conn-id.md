---
id: BL-799
title: Count each --retry attempt in %{xfer_id} and %{conn_id}
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-799 — Count each --retry attempt in %{xfer_id} and %{conn_id}

## Goal

After `--retry` runs a transfer again, `%{xfer_id}` and `%{conn_id}` print the numbers curl 8.21.0 prints, which count every attempt, instead of the first attempt's numbers.

## Context

- Found while doing BL-513. Measured 2026-09-28 with curl 8.21.0 (Windows, Schannel) through `Record-CurlExchange.ps1 -Connections 3`: `curl -s --retry 2 --retry-delay 1 -w '%{json}' http://127.0.0.1:18513/` against 503, 503, 200 printed `"conn_id":2` and `"xfer_id":2` for the only URL; against three 503s, the same. curl's retry makes a new transfer (and a new connection, the 503s here closing theirs) for each attempt.
- `Curl.Console/CurlCommandRunner.cs` builds `TransferWriteOutVariables` with `transfer.TransferId` (`UrlTransfer`), fixed per URL, and the retry loop is `FollowRetryingAsync`, whose `retrying` callback already counts retries into `RunningTransferState.RetryCount` (BL-513).
- Measure first how a later URL's `xfer_id` continues after an earlier URL was retried, and what `conn_id` prints when the retried attempts reuse one connection (a 503 with keep-alive).

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1`: the two cases above, a second URL after a retried first one, and a keep-alive 503; stdout and exit code copied into Notes.
- [x] `Curl.Console.UnitTests` pin each measured `xfer_id` and `conn_id` end to end on a fake `TimeProvider`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

Measured 2026-09-29 with curl 8.21.0 (Windows, Schannel, the mingw64 reference build) through
`Record-CurlExchange.ps1`, `-s --retry 2 --retry-delay 1 -w '%{xfer_id} %{conn_id}\n'`:

| Case | Server | stdout | exit |
| --- | --- | --- | --- |
| 503, 503, 200, each `Connection: close` | `-Connections 3` | `2 2` | 0 |
| three 503s, each `Connection: close` | `-Connections 3` | `2 2` | 0 |
| the same URL twice; the first gets 503, 503, 200, the second 200 | `-Connections 4` | `2 2` then `3 3` | 0 |
| 503, 503, 200 on one kept-alive connection | `-Script` (read/send x3) | `2 0` | 0 |

The keep-alive transcript shows all three `GET /` on one connection, so a retry that reuses the
connection keeps its `conn_id`, while `xfer_id` still counts every attempt.

Implementation (`Curl.Console` only): the `--retry` callback in `FollowRetryingAsync` calls
`TakeNextAttemptIds`, which fixes the retried attempt's `conn_id` (taking a number if no event took
one), clears it for the next attempt and gives the next attempt `nextTransferId++`
(`RunningTransferState.RetryTransferId`, which `-w` prints in place of `UrlTransfer.TransferId`).
After each attempt, `KeepRetriedConnectionIdWhenReused` gives back the retried attempt's
`conn_id` when an `http`/`https` attempt's report counts no connection opened.

Choices (defaults, not measured):
- Only HTTP reports `TransferReport.ConnectionCount` (the other handlers leave it 0), so any other
  scheme's retried attempt, and an HTTP attempt with no report, is taken to have opened a new
  connection. Pinned by `RunAsync_ConnIdAfterARetriedFtpAttempt_TakesANewNumber` and
  `RunAsync_ConnIdAfterARetriedAttemptThatReportsNothing_TakesANewNumber`.
- The `--trace-ids` markers still carry the first attempt's `xfer_id`, and under `--trace-ids` a
  reused retry connection has already taken a new number for its markers before its report says
  it was reused (the `-w` value is right; the counter skips one). Not measured; left as is, since
  the task is `-w`.

Tests: four measured cases in `CurlCommandRunnerRetryTests` (`ScriptedConnector.ReusesTheFirstConnection`
fakes the kept-alive connection), plus the two above over the new `ScriptedResultHandler`.
Measure-CodeQuality: Curl.Console 100% line, 100% branch, 741 members, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. %{xfer_id} and %{conn_id} count every --retry attempt, keeping conn_id on a reused connection, as curl 8.21.0 does
