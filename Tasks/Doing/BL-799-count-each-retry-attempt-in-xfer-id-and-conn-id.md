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
completed:
---
# BL-799 — Count each --retry attempt in %{xfer_id} and %{conn_id}

## Goal

After `--retry` runs a transfer again, `%{xfer_id}` and `%{conn_id}` print the numbers curl 8.21.0 prints, which count every attempt, instead of the first attempt's numbers.

## Context

- Found while doing BL-513. Measured 2026-09-28 with curl 8.21.0 (Windows, Schannel) through `Record-CurlExchange.ps1 -Connections 3`: `curl -s --retry 2 --retry-delay 1 -w '%{json}' http://127.0.0.1:18513/` against 503, 503, 200 printed `"conn_id":2` and `"xfer_id":2` for the only URL; against three 503s, the same. curl's retry makes a new transfer (and a new connection, the 503s here closing theirs) for each attempt.
- `Curl.Console/CurlCommandRunner.cs` builds `TransferWriteOutVariables` with `transfer.TransferId` (`UrlTransfer`), fixed per URL, and the retry loop is `FollowRetryingAsync`, whose `retrying` callback already counts retries into `RunningTransferState.RetryCount` (BL-513).
- Measure first how a later URL's `xfer_id` continues after an earlier URL was retried, and what `conn_id` prints when the retried attempts reuse one connection (a 503 with keep-alive).

## Acceptance criteria

- [ ] Measured with `Record-CurlExchange.ps1`: the two cases above, a second URL after a retried first one, and a keep-alive 503; stdout and exit code copied into Notes.
- [ ] `Curl.Console.UnitTests` pin each measured `xfer_id` and `conn_id` end to end on a fake `TimeProvider`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
