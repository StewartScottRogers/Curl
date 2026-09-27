# ADR-0045 — The transfer context carries a progress sink for the progress meter

- **Status:** Accepted
- **Date:** 2026-09-26
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-102 made `Curl.Console` write the start of curl 8.21.0's progress meter to stderr
after each successful transfer (`Curl.Console/ProgressMeterLines.cs`,
`CurlCommandRunner.WriteProgressMeterAsync` and `ShowsProgressMeter`). It can do no
more than that, because a handler reports nothing until it returns its
`TransferResult`. Two gaps need information from inside the transfer. Both were
measured on 2026-09-26 with curl 8.21.0 (x86_64-w64-mingw32) against a local
`python -m http.server`, with stderr redirected to a file:

1. **Live counters.** For a network transfer curl rewrites the status line in place.
   The updates are separated by `\r` with no newline between them, and one newline
   ends the line. A 10-byte file gave:

   ```
   \r  0      0   0      0   0      0      0      0                              0\r100     10 100     10   0      0    244      0                              0\r100     10 100     10   0      0    241      0                              0\r100     10 100     10   0      0    238      0                              0\r\n
   ```

   A 200000-byte file gave lines such as
   `\r100 195.3k 100 195.3k   0      0 46.52M      0                              0`.
   Drawing those counters needs the bytes received and sent so far, and the expected
   total when it is known.
2. **The meter after a failure.** curl prints the meter when the transfer got past
   connect or open and then failed. `curl -C 5 http://...` against a server without
   range support prints the meter, then
   `curl: (33) HTTP server does not seem to support byte ranges. Cannot resume.`
   It does not print the meter when the transfer failed before that point: a `file://`
   URL naming a missing file prints only `curl: (37) Could not open file ...`. The
   console needs a signal that the transfer has started.

`ITransferContext` is the only channel from `Curl.Console` into a handler (ADR-0003,
ADR-0006, ADR-0008). Those ADRs are Accepted and so immutable
(`Documentation/Planning/Decisions/README.md`); this is a new decision alongside them.

## Decision

`Curl.Protocol.Abstractions.UnitLibrary` gains a new interface, `ITransferProgress`,
with exactly these members, none of them `async` and all returning `void`:

- `void ReportTransferStarted()` - the transfer is past connect or open: a connection
  is established, or the source is open. Called at most once per transfer; the
  consumer ignores a repeat.
- `void ReportDownloaded(long bytesSoFar, long? expectedTotal)` - `bytesSoFar` is the
  running total of body bytes received in this transfer, never a delta, and never
  decreasing. `expectedTotal` is the body size the handler expects to receive (an
  HTTP `Content-Length`, for example), or `null` when it is not known.
- `void ReportUploaded(long bytesSoFar, long? expectedTotal)` - the same for body bytes
  sent: the running total, and the size of the upload when it is known, or `null`.

It gains one implementation, `NoTransferProgress`, a sealed class whose members do
nothing, reached through its single `NoTransferProgress.Instance` property.

`ITransferContext` gains exactly one member:

- `ITransferProgress Progress { get; }` - where the handler reports progress. Never
  `null`.

`TransferContext` implements it as an `init` property defaulting to
`NoTransferProgress.Instance`, the pattern ADR-0008 used for `ConnectTimeout` and
`MaxTime`. The interface member has no default implementation, so any class that
implements `ITransferContext` directly must say where its progress goes.

The rules for the sink:

- **The sink does not read time.** Neither `ITransferProgress` nor any implementation
  of it in a protocol library reads a clock, and no member takes a timestamp. The rate
  and time columns are measured by the consumer in `Curl.Console`, which reads the
  injected `TimeProvider` (the same one it passes as `ITransferContext.TimeProvider`)
  when each report arrives.
- **Reporting is synchronous and cheap.** Handlers call the byte members from inside
  their copy loops, so no member is `async` and an implementation does no I/O on the
  calling thread beyond what it can do without blocking. Redrawing the status line at
  curl's rate is the consumer's job, not the handler's.
- **No handler is obliged to report bytes.** A handler that never calls the byte
  members leaves the counters at zero, which is correct for `file://`: curl's `file://`
  status line stays all zeros (BL-102's Notes). The `file://` handler reports
  `ReportTransferStarted()` once the source is open and reports no byte counts
  (BL-129).
- **A handler reports "started" before it can fail past connect or open.** The console
  prints the meter after a failure only when `ReportTransferStarted()` was called
  (BL-130).

Adding the interface, `NoTransferProgress` and the member is BL-134. The console's use
of it is BL-130 (meter after a failure), BL-131 (live counters) and BL-132 (the `-#`
bar). `RedirectFollower.NextHop` copies context members into each new hop by hand, and
the default means the compiler will not catch a missing copy, so carrying `Progress`
across redirect hops is its own task, BL-310.

## Consequences

Good:

- Every existing handler and test keeps compiling and passing unchanged: they build a
  `TransferContext` and get the do-nothing sink.
- Handlers stay testable without a clock: a test passes a recording `ITransferProgress`
  and pins what was reported, in order, without any timing.
- The console owns all of curl's display logic (the update interval, the rate
  averaging and the column formatting) in one place, on the injected `TimeProvider`.

Costs and caveats:

- `ITransferContext` grows by another member that no handler reads yet.
- A class that copies a context by hand (today only `RedirectFollower.NextHop`) drops
  the sink silently unless it copies `Progress`; BL-310 fixes the one that exists.
- Handlers report running totals, so a handler that retries or rewinds an upload (a
  followed PUT, ADR-0033) must decide what "so far" means for the retry; this ADR
  leaves that to the handler task that first rewinds while reporting.

## Alternatives considered

- **Report through `TransferResult` only.** Rejected: the result arrives after the
  transfer ends, which is too late for live counters and cannot say that a failed
  transfer had started.
- **An `IProgress<T>` from the BCL with a progress record.** Rejected: `IProgress<T>`
  says nothing about "started", and `Progress<T>` posts to a synchronization context,
  which is the asynchronous hop a copy loop must not pay for.
- **Timestamps passed with each report, or read by the sink.** Rejected: it would put a
  clock in every protocol library and make handler tests depend on time. The consumer
  already has the injected `TimeProvider`.
- **Deltas instead of running totals.** Rejected: a running total is idempotent, so a
  repeated or dropped report cannot put the counters out of step with the transfer.
- **A default interface implementation of `Progress` on `ITransferContext`.**
  Rejected: every class implementing the interface directly would silently report to
  nothing; with no default, the compiler makes each one choose.
- **A `null` sink meaning "nobody is listening".** Rejected: every handler call site
  would need a null check, and a missed one is a `NullReferenceException` in a copy
  loop. The do-nothing instance costs one virtual call.
