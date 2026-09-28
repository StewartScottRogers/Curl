# ADR-0092 — The progress meter's status lines are drawn from the handler's byte reports

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-131 made `Curl.Console` draw the progress meter's status lines from the handler's
byte reports: `TransferProgressRecorder` records the counts on the injected clock and
draws each line with `ProgressMeterFields`, replaying curl 8.21.0's `lib/progress.c`
(`progress_calc`, `progress_meter`, `max6out`, `time2str`). The source reading and the
measurements are in BL-131's Notes
(`Tasks/Done/2026-09-27_1215/BL-131-rewrite-the-progress-meter-status-line-in-place-with-live-co.md`).

In curl a line is drawn on the first update, after each new speed sample (1000 ms after
the latest), and when the request is done; `Curl_pgrsDone` forces one last update and
writes the closing line feed. A measured 10-byte HTTP download shows three done draws in
a row. A failed transfer reaches only `Curl_pgrsDone`, with the request not done.

## Decision

1. **The meter is still written after the transfer.** The recorder draws the lines as
   the reports come in, and the runner writes them to standard error when the transfer
   ends. Standard error's bytes are curl's; only a terminal differs, seeing the lines at
   the end rather than one line rewritten in place. *Why:* drawing live needs
   synchronous writes to standard error from inside the handler's reports, and the
   redirected, byte-identical stream is what scripts see. Live drawing on a terminal is
   BL-383.
2. **A successful transfer whose handler reported bytes gets three done draws; a failed
   one gets one `Curl_pgrsDone` update**, which draws only when a second or more has
   passed since the last speed sample. *Why:* three is the measured count for a
   completed transfer, and the single not-done update is what curl's `multi_done` path
   gives a failure.
3. **A handler that reports no bytes (`file://` today) gets no end draws**, so BL-102's
   measured `file://` bytes (the single zero line) are unchanged. *Why:* curl completes
   `file://` without the network transfer loop, so it never reaches those done draws.
4. **curl's same-second check in `progress_calc` (`lastshow`) is not modelled.** *Why:*
   every draw of a running transfer in this call sequence follows a new sample taken
   1000 ms or more after the one before, so the whole second always differs; the branch
   could never be taken, and could not be covered.

## Consequences

- Redirected standard error carries curl's status-line bytes for HTTP transfers, and
  `file://` keeps its pinned output.
- A person watching a terminal sees the meter appear all at once when the transfer ends,
  until BL-383 draws it live.
- If a later change adds a draw that can land within the same second as the one before,
  the `lastshow` check has to be added with it.

## Alternatives considered

- **Draw live on standard error now.** Rejected for BL-131: it changes how the handler's
  reports reach standard error, a separate piece of work (BL-383), and does not change
  the bytes scripts see.
- **One done draw, or the same done draws for every handler.** Rejected: one does not
  match the measured three, and giving `file://` end draws would change BL-102's
  measured bytes.
- **Model `lastshow` anyway.** Rejected: dead code that the 100% branch coverage gate
  could never reach.
