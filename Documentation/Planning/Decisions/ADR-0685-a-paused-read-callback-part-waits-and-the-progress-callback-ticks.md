# ADR-0685 — A paused MIME or form callback part waits for curl_easy_pause, and the progress callback ticks while paused

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2631.

## Context

Upstream cases 670 to 673 (lib670) post one MIME part (670, 671) or `curl_formadd` stream field (672, 673) of length 2
whose read callback gives `A`, returns `CURL_READFUNC_PAUSE`, and gives `B` once `curl_easy_pause(CURLPAUSE_CONT)`
has resumed it at least 5 seconds later. 670 and 672 unpause from their multi loop, 671 and 673 from their
`CURLOPT_XFERINFOFUNCTION` on the easy interface. The managed engine stopped after `A` in all four:

1. A `curl_mime_data_cb` part's `MimeCallbackReadStream` was built without the handle's `EasyPauseGate`
   (ADR-0573), so its pause could not be waited out. A form stream field is the same kind of part (ADR-0651).
2. The engine calls the progress callback only when the handler reports progress, so a paused upload never called it
   again and the easy interface had nothing to unpause from. libcurl's `curl_easy_perform` waits at most 1000 ms in
   `curl_multi_poll` and runs the transfer, calling the progress callback, each time it wakes, paused or not.

## Decision

1. `MultipartFormPartMapping.FromMimeBody` and `MimePostForm.BuildAsync` take the handle's pause gate, which
   `ManagedEasyTransfer` gives as it does an upload's (`PauseGateFor`: none for a `file` URL), and each callback
   part's stream waits on it as `CallbackReadStream` does.
2. While a perform waits in `EasyPauseGate.WhenResumedAsync`, it calls the perform's progress callback with the counts
   it last gave every `EasyPauseGate.PausedTickInterval` (1 second, libcurl's longest poll), measured on the engine's
   `TimeProvider` (`TickWhilePaused`, `CallbackTransferProgress.PausedTickOf`). Ticks run only while paused, so a
   transfer that never pauses calls the callback exactly as before, and libtest output that counts callback calls
   does not change. With no progress callback, or under `CURLOPT_NOPROGRESS`, nothing ticks.

## Consequences

Cases 670 to 673 pass and are held on the ratchet. A progress callback may now be called from the waiting read or
write, on a thread pool thread, while the transfer is paused; it may call `curl_easy_pause` there, as lib670 does.
