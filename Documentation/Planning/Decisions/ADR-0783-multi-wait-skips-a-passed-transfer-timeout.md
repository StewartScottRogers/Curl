# ADR-0783 — A multi wait or poll leaves out a transfer timeout that has already passed

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3206
- Decided by Claude under Stewart's delegation.

## Context

A `curl_multi_perform` and `curl_multi_poll(..., 100)` loop over one transfer with
`CURLOPT_TIMEOUT_MS` set spun at full CPU. Reproduced 2026-10-22 against the managed engine and a
loopback server that never answers, with `CURLOPT_TIMEOUT_MS` 300: the loop turned about every
110 ms until the timeout passed, then more than 100,000 times in the ~140 ms before the transfer
ended with `CURLE_OPERATION_TIMEDOUT`.

`ManagedMultiTransfer.GetTimeout` answers 0 once a running transfer's timeout has passed, as
`curl_multi_timeout` does, and the wait capped itself by that value, so it returned at once. But
the managed engine's transfer is not ended by the multi handle's timer: its perform ends itself
with exit 28 through its own max-time watchdog, a moment later. Until then every wait returned at
once and every perform found nothing done. libcurl ends the transfer in the very
`curl_multi_perform` that follows its expired timer, so it never sees that window.

## Decision

`WaitAsync` and `PollAsync` cap their wait with `ManagedMultiTransfer.WaitTimerMilliseconds`: 0
while a perform has work, else the time until the first running transfer's timeout still ahead,
leaving out one that has passed, else -1. The wait already ends when a running perform finishes,
so once a timeout has passed the wait ends as the transfer's perform ends with exit 28, or at the
caller's timeout. `GetTimeout` keeps answering 0, as `curl_multi_timeout` does.

Ending the transfer from the multi handle at the instant its timer passes (cancelling its perform
and queueing exit 28 itself) was rejected: the message would lose the perform's own error text
(`Operation timed out after N milliseconds with M bytes received`) and its transfer info, which
the watchdog's ending keeps.

## Consequences

The loop turns about once per wait, also after the timeout. The transfer still fails with exit
28 from its own perform. The pinned values are the engine's computed wait, not text libcurl
prints, so no libcurl measurement was needed to pin them. The timer callback
(`CURLMOPT_TIMERFUNCTION`) still reports the handle's timer as `GetTimeout` gives it.
