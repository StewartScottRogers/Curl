# ADR-0082 — The `-#` progress bar replays curl's callback from the handler's reports

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-132.

## Context

Under `-#`/`--progress-bar` curl 8.21.0 draws a bar instead of the progress meter. The bar
is drawn by `tool_progress_cb` in `src/tool_cb_prg.c`, which libcurl calls as the transfer
runs, and a newline follows it in `post_per_transfer` (`src/tool_operate.c`) when the
callback ran at least once. Curl.Console has no libcurl callback; it has what each handler
reports through `ITransferProgress` (ADR-0045), and it already writes the meter after the
transfer from those reports (BL-131). Three choices had to be made:

1. What stands for a call of curl's callback.
2. What a handler that reports no bytes, as the `file://` handler does not, draws. curl's
   `file://` transfer ends with a call whose received bytes and total are both the file's
   size, so a ten-byte file ends on a full bar and an empty one on no bar at all
   (measured, BL-132 Notes).
3. Where the width comes from. curl re-reads `get_terminal_columns()` on every call.

## Decision

- `ProgressBarRecorder` follows `tool_progress_cb` step by step. `ReportTransferStarted` is
  the first call, with no bytes. Each `ReportDownloaded` and `ReportUploaded` is a call with
  the reported counts, and an unknown size counts as curl's 0. The resolved `-C` offset is
  curl's `initial_size`. The 100 ms limit, the full bar that is always drawn at 100%, and
  `fly`'s animation for an unknown size are timed on the runner's `TimeProvider`.
- A successful transfer whose handler reported no bytes gets one more call, with its
  `TransferResult.BytesTransferred` as both the bytes and the total. This reproduces every
  `file://` case measured (ten bytes, `-C 5`, `-C 10`, an empty file) without changing the
  `file://` handler, whose meter output depends on it reporting no bytes (BL-129, BL-131).
- The width is the runner's `terminalColumns` (`TerminalColumns.Resolve`, the same
  `COLUMNS`-then-console-then-79 rule as `get_terminal_columns`), clamped to 20..400 as
  `update_width` does. It is resolved once per run.
- The bar is written after the transfer, as the meter is. The newline is written after the
  transfer's failure lines and before its `-w` output, where curl writes it.

## Consequences

- The bytes on standard error match curl for every case measured in BL-132, and for any
  handler whose reports match libcurl's counters.
- A terminal does not see the bar move while the transfer runs. The meter has the same
  limitation (BL-131).
- A console that is resized during a run keeps the width it had when the run started.
- If the `file://` handler starts to report bytes, its bar is still right, because the
  extra call is made only when no bytes were reported.

## Alternatives considered

- **Make the `file://` handler report its bytes.** This lost because it changes the meter,
  which curl 8.21.0 leaves at the zero line for `file://`. The handler is also outside
  `Curl.Console`.
- **Always draw a full bar after a success.** This lost because curl draws no bar for an
  empty file, only the newline.
- **Re-read the terminal width on every call.** This lost because the runner is given one
  width, and the only difference would be a console resized in the middle of a transfer.
