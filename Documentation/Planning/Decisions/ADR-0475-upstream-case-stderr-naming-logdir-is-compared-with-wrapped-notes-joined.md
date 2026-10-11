# ADR-0475: An upstream case's `<stderr>` naming `%LOGDIR` is compared with curl's wrapped `Note:` and `Warning:` lines joined

- Status: Accepted
- Date: 2026-10-10
- Task: BL-2038 (split from BL-2006, gap finding GF-0013)
- Decided by Claude under Stewart's delegation

## Context

curl 8.21.0's `warnf` and `notef` (`voutf` in `src/tool_msgs.c`) wrap a message longer than
`COLUMNS` - 79 under `runtests.pl` (line 172) - after its last blank, or at the full width when
there is none, and repeat the `Note: ` or `Warning: ` prefix on each new line. Curl does the
same (`WarningLineWrapper`, `WrappedMessage`).

`runtests.pl`'s `%LOGDIR` is the short relative `log`, so test994, test996 and test1491's
`Note: skips transfer, "log/there" exists locally` fits on one line and their `<stderr>` pins it
unwrapped. The harness's `%LOGDIR` is absolute (ADR-0458) and about 100 characters long, so the
same note is wrapped, and the three cases failed at byte 22 of their stderr although Curl's bytes
are curl's for the same directory.

Two ways out were weighed:

1. Widen `COLUMNS` for the run by the length the absolute `%LOGDIR` adds over `log`. Simpler, but
   it also widens every message that does not name the log directory, so a case pinning a
   79-column wrap would stop checking it.
2. In verification, when the expected `<stderr>` names the case's `%LOGDIR`, join curl's wrapped
   `Note:` and `Warning:` lines in both expected and actual stderr before comparing.

## Decision

The second. `UpstreamCaseRun.LogDirectory` carries the case's `%LOGDIR`, and
`UpstreamCaseVerification` passes both sides of the `<stderr>` comparison through
`UpstreamWrappedMessageLines.Unwrap` only when the expected body contains it. A line is joined to
the next when both start with the same `Note: ` or `Warning: ` prefix and the first ends in a blank
or tab or is at least 79 characters long (`voutf`'s two cuts); the next line's prefix is dropped.

## Consequences

- test994, test996 and test1491 pass and are listed in `PassingUpstreamCases.txt`.
- Only stderr that names the log directory is joined, so the wrap of every other message is still
  compared byte for byte. Where it is joined, the wrap position itself is no longer checked; Curl's
  wrap is pinned by its own unit tests instead.
