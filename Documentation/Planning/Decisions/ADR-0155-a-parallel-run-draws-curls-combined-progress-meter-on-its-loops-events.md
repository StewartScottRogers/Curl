# ADR-0155 — A parallel run draws curl's combined progress meter on its loop's events

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-521.

## Context

Under `-Z`, curl 8.21.0 does not draw a progress meter per transfer. It draws one combined meter
for the whole run (`progress_meter` in `src/tool_progress.c`). The meter has its own header line,
`DL% UL%  Dled  Uled  Xfers  Live Total     Current  Left    Speed`, and status lines that each
start with a carriage return. Sizes are five columns wide (`max5data`) and times eight
(`time2str`, blank for zero). The final line ends with a line ending. The loop in
`parallel_transfers` calls `progress_meter` after each wake-up: socket activity, a transfer ending,
or its one-second poll timeout. It draws only when more than 500 ms have passed since the last
draw, and it always draws the final line.

curl 8.21.0 (Schannel, Windows 11) was measured on 2026-09-28 with
`Record-CurlExchange.ps1 -Connections 2 -ResponseDelayMilliseconds 1500`. The runs were
`curl -Z A B`, `--parallel-immediate`, `-#`, three URLs, `-s` and `--no-progress-meter`, all to one
loopback host. Each answer was ten bytes. For sizes, `file://` downloads of 300000, 10000000 and
300000000 bytes were measured, two per run. The standard error bytes are in the BL-521 Notes.
`-#` is ignored under `-Z`, and `-s` and `--no-progress-meter` write nothing.

## Decision

1. `Curl.Output`'s `ParallelProgressMeterText` formats the header and status lines as
   `progress_meter` does, with `max5data`'s units `k` to `P` and `time2str`'s `hh:mm:ss`,
   `ddd hhh` and `ddddddd`. The line layouts past 99 hours are from curl's source and were not
   measured.
2. `Curl.Console`'s `ParallelProgressMeter`, which the run's `ParallelRun` owns, computes the
   figures as `progress_meter` does:
   - the bytes of the ended transfers plus the bytes of the running ones;
   - a percentage only when every transfer that has not ended knows its size (a size of zero counts
     as unknown, as curl's `dltotal` does);
   - a speed over the last ten samples, or since the run started until ten samples exist;
   - time estimates from the run's total download size.
3. A line is drawn when the loop would draw one, if more than 500 ms have passed since the last:
   - a handler's byte report;
   - a transfer ending (after its report);
   - the runner having started every transfer it can (no `--parallel-max` slot free, or every
     transfer started);
   - one second without a draw attempt.

   The final line is drawn at the end of the run, after the deferred `--fail-early` reports. A
   transfer going live does not draw, so the first line counts every transfer the runner has
   started (`Xfers`), as curl's first line does.
4. `Xfers` counts the transfers the runner has taken a slot for. curl creates transfers ahead of
   their slots, up to twice `--parallel-max`, so the two counts differ only while more transfers are
   queued than `--parallel-max`. `Live` counts the transfers past their host wait (ADR-0153) that
   have not ended, as curl counts `added`.
5. Under `-Z` no per-transfer meter or `-#` bar is drawn. The combined meter is shown unless the
   runner writes no meter, or the first option group has `-s` or `--no-progress-meter`, as curl's
   global flags decide.

## Consequences

- A `-Z` run's standard error has curl's layout, header and counts. The exact line breakdown
  depends on timing, as curl's does, and the unit tests pin it on a manual clock that replays the
  measured run.
- The idle draw uses a timer on the runner's `TimeProvider`, which is disposed when the run ends.
- Not measured and left as curl's source has it: a body on a terminal under `-Z`, and the meter's
  interleaving with `-v` output.

## Alternatives considered

- **Keep one meter per transfer.** It was rejected because curl never draws per-transfer meters
  under `-Z`, so the bytes would differ in every case.
- **A fixed 500 ms timer.** It was rejected because it draws lines curl does not. curl's loop wakes
  on activity or after a second, and the measured lines follow that.
