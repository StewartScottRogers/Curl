# ADR-0902: A failed write to a -D, --trace, -c, %output or --stderr file is handled as curl handles it

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3830
- Decided by Claude under Stewart's delegation.
- Extends ADR-0815.

## Context

Third-party gap report issue CGR-026: on Linux a file on a full device (`/dev/full`, ENOSPC)
takes writes into its buffer and fails when flushed or closed. BL-3555 fixed the `-o` file. For
the others Curl let the close's `IOException` escape to `UnhandledExceptionGuard` (ADR-0766), exit
2 with `curl: (2) No space left on device`. curl 8.18.0 (OpenSSL, WSL), measured on 2026-10-26
with `-sS` and `file:///tmp/s.txt` (12 bytes):

- `-D /dev/full -o /dev/null`: `curl: Failed writing headers to /dev/full`, then
  `curl: (23) client returned ERROR on write of 20 bytes`, exit 23 (17 bytes over HTTP, the status line).
- `--trace /dev/full`, `--trace-ascii /dev/full`, `-c /dev/full` (HTTP) and
  `-w '%output{/dev/full}x'`: exit 0, nothing on standard error.
- `--stderr /dev/full` with a failing transfer: the transfer's own exit code (7 for a closed port).
- `-o /dev/full` with a 20000-byte body: `file://` says
  `(23) Failure writing output to destination, passed 16384 returned 0`; over HTTP the count is
  the first body chunk curl hands on, `passed 16342` when the head and body arrive in one read
  (`nc` sending one buffer) and `passed 16384` when the body arrives after the head (the report's
  Python server).

## Decision

- `StandardErrorFailureIgnoringStream` becomes `WriteFailureIgnoringStream` and also ignores an
  `IOException` from closing the stream it wraps. The `--stderr` file, the `--trace` and
  `--trace-ascii` file and every `%output{file}` target (`WriteFailureIgnoringWriteOutFileOpener`,
  wrapping whatever opener the runner is given) are written through one, as curl ignores
  `fwrite`, `fflush` and `fclose` failures on all of them.
- The `-D` file is closed through one: its header writes still fail at once, through
  `DumpHeaderOutputStream`, which prints `Failed writing headers` and fails the transfer with the
  handler's exit 23; only the close, whose result curl ignores, is quiet.
- `CookieStore.SaveCookieJarAsync` counts a close that fails as a jar that could not be written,
  which is already silent (curl prints nothing, even under `-v`).
- The large body's count is not changed. Curl's HTTP reader hands on its read-ahead's leftover body
  bytes first, as curl does, so its count matches curl's for the same arrival of bytes; the
  report's `16342` against `16384` is the difference between the two runs' TCP segments, not
  between the tools. `file://` passes 16384, as curl does (pinned in `Cgr026FollowUpTests`).

## Consequences

A full device behind any of these files no longer ends the run with exit 2. A run that writes a
trace or `%output` file which fails gets no message, as with curl, so the failure is silent.
