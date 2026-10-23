# ADR-0764 — A write failure keeps the transfer's report for -w

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3170
- Decided by Claude under Stewart's delegation.

## Context

A transfer whose output cannot be written ends with exit 23. Curl replaced the handler's result
with a fresh failure that carried no `TransferReport`, so `-w` printed `000 0` for a response that
had arrived. curl 8.22.0, measured on 2026-10-22 with `Record-CurlExchange.ps1` (a 200 response,
5-byte body, 38 header bytes), prints the transfer's own values:

| Case | stderr | `-w '%{http_code} %{size_download} %{size_header}'` |
| --- | --- | --- |
| standard output opened read-only (`1<file`) | `curl: Failed writing body` | `200 5 38` |
| `-o` naming a directory | `curl: (23) client returned ERROR on write of 5 bytes` | `200 0 38` |
| `-D` naming a directory | `curl: Failed to open ...`, `curl: (23) Failed writing received data to disk/application` | `000 0 0` |

## Decision

1. Standard output: the runner lays `StandardOutputWriteFailure`'s exit code and message over the
   handler's result (`with`), so its report and byte count stay. `FormatErrorLine` now compares the
   result's message by reference with `StandardOutputWriteFailure.ErrorMessage`, which only this
   path sets, so the `curl: Failed writing body` line is unchanged.
2. `-o`: `DeferredOutputFileStream.CompleteAsync` and `SettleRetriedAttempt` keep the handler's
   `Report` on their `client returned ERROR on write of N bytes` failure. The report's download size
   already counts no body byte the file failed to take, matching curl's `0`.
3. `-D`: the file opens before the transfer, so nothing arrives and `000 0 0` is already right.

## Consequences

`-w` after an exit 23 write failure reports what arrived, as curl does; the failure's exit code and
message are unchanged. Pinned by `CurlCommandRunnerWriteOutAfterWriteFailureTests`.
