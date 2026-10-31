# ADR-1169: The ratchet runs lib670 over a clock that skips its pause

- Status: Accepted
- Date: 2026-10-31
- Task: BL-4378
- Decided by Claude under Stewart's delegation.

## Context

Upstream's `lib670.c` (cases 670 to 673) pauses its read callback and unpauses only once the pause has
lasted `PAUSE_TIME` (5) seconds by `time(NULL)`. It gives up with `unpausing failed: drain problem?` at
four times that. `Lib670` reads that time from the `TimeProvider` it is given, and
`LibTestPrograms.Ported` gives it `TimeProvider.System`. So every run of these cases waits 5 seconds of
real time. In the multi case (670) that wait is a loop of `curl_multi_perform` and 500 ms
`curl_multi_wait` calls. Curl's multi engine runs each transfer callback through a queue that blocks a
thread-pool thread until a perform takes it (`MultiCallbackQueue`). On a busy Windows CI runner, with the
whole ratchet running method-parallel, the thread pool was starved. test670 then ran past the ratchet's
30-second `CaseHangLimit` twice in a row (CI run 41151657472) and failed, between passing runs.

BL-4378 measured it. `EasyPauseGate` swaps its resume signal and its directions under one lock. A
callback's pause is dropped when an unpause came while the callback ran. So no unpause can be missed. In
eleven runs of the case under eight parallel test processes, every run passed in 5 seconds. The stall was
load on real time, not a wait in Curl.

## Decision

The ratchet's table of libtest programs (`UpstreamConformanceTests.LibTests`) replaces `lib670` with
`Lib670WithItsPauseSkipped`. That type runs `Lib670` over a new `PauseSkippingClock` for each run. The
clock is the system clock, except that every reading after the first is `Lib670.PauseSeconds` later. The
first reading is the moment the pause begins.

- The pause still holds until the program calls `curl_easy_pause(CURLPAUSE_CONT)`. The case still checks
  that Curl calls the read callback again after the unpause, sends `B`, and ends the post.
- The 5 seconds pass at once. test670 now takes about 0.4 seconds, not 5. The real time left before the
  20-second give-up is 15 seconds, and the hang limit sits beyond that, so a loaded runner can only fail
  the case where Curl itself does not resume.
- A clock for each run keeps concurrent runs of cases 670 to 673, and a listed case's second run, apart.

## Consequences

- One check is lost in the ratchet. Under this clock, a read callback that Curl called again *before* the
  unpause would also see a 5-second pause and send `B`, not `A`. That check stays covered by `Lib670`'s
  own tests (`Lib670ReadThis_TooShortAPause_GivesAAgain`) and by the engine's pause tests.
- `LibTestPrograms.Ported` and `Lib670` are unchanged. Only the conformance run's view of time changes.
