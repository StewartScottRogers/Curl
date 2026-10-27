# ADR-0816: curl_multi_cleanup inside a transfer callback is refused with CURLM_RECURSIVE_API_CALL

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3541
- Decided by Claude under Stewart's delegation.

## Context

Third-party gap report issue CGR-012: a write callback that calls `curl_multi_cleanup` on the multi
handle running its transfer never returned in Curl. `ManagedMultiTransfer.DisposeAsync` awaited every
transfer's perform, including the one the callback runs in, so it waited on itself; the application's
next `curl_multi_perform` then threw `ObjectDisposedException`. libcurl 8.22.0 checks
`Curl_is_in_callback` first and answers `CURLM_RECURSIVE_API_CALL` (8) without doing anything, and the
transfer completes. `CurlMulti` had only `DisposeAsync`, which answers no code.

## Decision

- `IMultiTransfer.IsInTransferCallback` (a default member answering `false`) tells whether the code
  asking runs inside one of the handle's transfers. `ManagedMultiTransfer` answers it from an
  `AsyncLocal` its `Start` sets before calling the transfer's `PerformAsync`: the value flows into the
  perform and every callback it makes, and never back out to the application's own calls.
- `ManagedMultiTransfer.DisposeAsync` does nothing when called from inside one of its own transfers.
- `CurlMulti.CleanupAsync` is `curl_multi_cleanup` with its answer: `BadHandle` once cleaned up,
  `RecursiveApiCall` from inside a transfer callback (the handle untouched), else `Ok`.
  `DisposeAsync` calls it and drops the answer.

## Consequences

The callback's cleanup returns 8 and the transfer finishes with its `CURLMSG_DONE`, as REF does; a
later cleanup outside the callback answers `Ok`. Only transfer callbacks count as "in a callback";
the socket and timer callbacks of the multi handle itself are not covered by this check.
