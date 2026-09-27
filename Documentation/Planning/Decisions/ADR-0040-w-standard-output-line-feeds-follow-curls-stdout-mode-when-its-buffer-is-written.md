# ADR-0040 — -w standard output line feeds follow curl's stdout mode when its buffer is written

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (task BL-235, 2026-09-26).

## Context

On Windows, curl 8.21.0 (mingw, Schannel) opens standard output, standard error and
`%output{file}` targets in text mode, where the C runtime writes each line feed as CR LF. It
switches standard output to binary mode when it sets up a transfer whose body goes there.
Standard output is a buffered C `FILE`, and the translation happens when the buffer is
written out, so a `-w` line feed already in the buffer is written in whatever mode standard
output is in by then. Measured on 2026-09-26 (commands in BL-235's Notes):

| Command (`W` = `-w "%{exitcode}\n"`) | Standard output |
| --- | --- |
| `W http://…` (body to stdout) | `hello200\n` |
| `-o NUL W http://…` | `200\r\n` |
| `-o NUL -o NUL W f f g` (g to stdout) | `0\n0\n` then g's body and `0\n` |
| `-o NUL W <refused> g` | `7\n` then g's body and `0\n` |
| `--fail-early -o NUL -o NUL W <refused> f g` | `7\r\n` |
| `-D <dir> W f` (f to stdout) | `23\r\n` |
| `-o NUL -C 3 -o <dir> W f f g` | `0\r\n23\r\n` |

Standard error is always CR LF: `-sS -f -w "%{stderr}%{http_code}\n"` gives
`curl: (22) …\r\n404\r\n`. Linux and macOS translate nothing.

`WriteOutTemplateRenderer` (`Curl.Output`) has one `writesLineFeedAsCrLf` switch for all three
targets, which cannot express a binary standard output beside a text-mode standard error.

## Decision

`Curl.Console` constructs the renderer with `writesLineFeedAsCrLf: false` and wraps each
text-mode target in `LineFeedToCrLfStream`: standard error always on Windows, and standard
output on Windows unless `CurlCommandRunner.IsStandardOutputBinaryForWriteOut` says it is
binary. That is when this or an earlier transfer sent its body to standard output (a URL with
no `-o` whose `-D` file opened), or when the run goes on after this transfer and a later URL
has no `-o`. The `%output{file}` opener (BL-280) wraps its files the same way on Windows.

## Consequences

Every measured row above is reproduced except the last: a later transfer whose resumed `-o`
file cannot be opened ends the run before a standard-output URL, which curl writes as CR LF
and this predicts as LF. That needs a resume failure and a standard-output URL after it in
one run, with `-w`; it is left unmodelled. A standard output that fills curl's 4096-byte
buffer while still in text mode is not modelled either.

## Alternatives considered

- **Buffer `-w` standard output until the run ends, then translate.** Exact, but it would
  reorder the `-w` text against the bodies that follow it on the same stream.
- **Set the renderer's switch per transfer.** Cannot give standard error CR LF and standard
  output LF in the same rendering.
