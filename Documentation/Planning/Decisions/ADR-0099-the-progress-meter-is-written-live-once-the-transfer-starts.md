# ADR-0099 — The progress meter is written live once the transfer starts

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

ADR-0092 (BL-131) drew the progress meter's status lines as the handler reports bytes but
wrote them all to standard error after the transfer, so a terminal saw the meter appear at
the end instead of one line rewritten in place. BL-383 writes them while the transfer runs.

curl 8.21.0 writes the header lines at its first draw and flushes every status line.
Three things constrain doing the same here:

- `ITransferProgress` reports are synchronous, and the solution forbids `.Result` and
  `.Wait()`, so a report cannot await a write.
- The first draw (the zero line) is made when the transfer's recorder is made, before the
  handler has connected, but a transfer that fails before the handler reports it past
  connect or open shows no meter at all (BL-130), so nothing may be written yet.
- `-s`, `--no-progress-meter`, `-#` and a body on a terminal hide the meter, and those are
  all known before the transfer starts.

## Decision

1. **The runner gives the recorder a live writer only when the meter is shown**
   (`ShowsProgressMeter`, known before the transfer). *Why:* every hiding rule is known up
   front, so nothing is ever written that would later have to be withheld.
2. **Nothing is written until the handler first reports the transfer started.** From that
   report on, the recorder hands the writer every line drawn so far, then each line a report
   draws. *Why:* once started, BL-130's rule says the meter is shown whatever the outcome,
   so writing early can never be wrong; before it, the outcome is unknown.
3. **The write is a synchronous `Stream.Write` and `Flush` of the UTF-8 text**, with the
   header lines (and any `** Resuming` line) in front of the first write, taken through the
   same once-per-transfer rule `--retry` uses. *Why:* the report is synchronous, and the
   text is already built, so no async path is needed.
4. **What the transfer draws as it ends (`Finish`, `FinishRedirectHop`), the closing
   newline, and the whole meter of a transfer never reported started are written after the
   transfer, as before.** The runner writes only the lines not yet written. *Why:* the
   standard-error bytes stay exactly BL-131's; only when they reach the stream changes.

## Consequences

- A terminal sees the meter's header and zero line when the handler connects and each
  status line as it is drawn, as curl shows it.
- Redirected standard error carries the same bytes as before, pinned by the existing
  progress-meter tests, which pass unchanged.
- A handler that reports bytes without ever reporting the transfer started (none today
  among those that report bytes) still shows its meter only at the end.
- Under `-v` the verbose lines and the meter now interleave in time order on standard
  error, as they do in curl, instead of the meter following all of the verbose lines.

## Alternatives considered

- **Write the header and zero line at the first draw, as curl does.** Rejected: that draw
  happens before the handler connects, and a transfer that then fails to connect must show
  nothing (BL-130).
- **Queue async writes from the synchronous report.** Rejected: ordering against the
  runner's own writes would need extra synchronization for no gain in bytes.
