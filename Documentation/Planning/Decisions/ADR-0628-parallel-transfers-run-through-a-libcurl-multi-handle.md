# ADR-0628 — `-Z` transfers run through a `Libcurl` multi handle

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2453.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` has `--parallel` run its transfers through `Libcurl`'s
`CurlMulti`, as curl's `tool_operate.c` adds each parallel transfer's easy handle to one multi handle. Since
ADR-0624 a serial transfer runs through a `CurlEasy`, but a `-Z` transfer still performed each attempt directly
(`TransferPerformer.FollowWatchingSpeedAsync` on a context the runner built). Measured against the code as it is now:

1. The runner's `-Z` scheduling - `--parallel-max` (`ParallelTransferQueue`), `--parallel-max-host` and
   `--parallel-immediate` (`ParallelHostQueue`), the combined meter, `--fail-early` and the report order - is the
   tool's, and runs as one flow per transfer, each with its own `RunningTransferState` in an `AsyncLocal`.
2. The engine's `ManagedMultiTransfer` runs each added transfer as its own `IEasyTransfer.PerformAsync`, started by
   the next `curl_multi_perform`, in the flow that called it; it ends `curl_multi_poll` when a perform completes or
   `curl_multi_wakeup` is called, and queues a `CURLMSG_DONE` message per finished transfer.
3. A multi handle is for one flow at a time, and the engine's keeps its transfers in plain lists.

## Decision

1. A `-Z` run holds one `CurlMulti` from `ToolCurlEngine.MultiInit` (`curl_multi_init` on the runner's library),
   wrapped in `ParallelTransferMulti` and kept as `ParallelRun.Transfers`; `ParallelRun.EndAsync` cleans it up.
2. Every transfer, serial or `-Z`, sets up one easy handle as ADR-0624 describes. A `-Z` attempt, `--retry` attempts
   included, calls `ParallelTransferMulti.PerformAsync`: `curl_multi_add_handle` and `curl_multi_perform` in the
   transfer's own flow, so its engine transfer starts there and sees that transfer's state; one loop, started in no
   transfer's flow while any attempt waits, then polls, performs and reads every `CURLMSG_DONE` with
   `curl_multi_info_read`, removes that handle and hands its result to the attempt.
3. Every call on the multi handle is made holding one gate; an attempt about to add calls `curl_multi_wakeup` first,
   so the loop's poll ends rather than holding the gate until a transfer finishes.
4. The tool's scheduling stays where it is: no `CURLMOPT_MAX_HOST_CONNECTIONS` or `MAX_TOTAL_CONNECTIONS` is set, so
   the multi handle starts each transfer the moment it is added and the tool's queues alone decide when that is.
5. A perform that throws ends every waiting attempt with the exception, as a transfer that faulted already ended the
   run at once.
6. A `-Z` transfer's `-w` values now come from its handle's `curl_easy_getinfo` answers too (ADR-0625, decision 2),
   as it has a handle.
7. `ToolCurlEngine.EasyInit` pairs the handle with its engine transfer under a lock, as `-Z` transfers ask for handles
   from several flows.

## Consequences

- Every existing `-Z` test and the upstream parallel cases on the ratchet pass unchanged; the bytes are the same.
- `PerformDirectlyAsync` is gone: the runner no longer builds an attempt's context itself.
- The tool's own queues still decide when a `-Z` transfer starts; moving `--parallel-max-host` onto
  `CURLMOPT_MAX_HOST_CONNECTIONS` would need the engine's pending transfers to start in their own flow, and is not
  done here.
