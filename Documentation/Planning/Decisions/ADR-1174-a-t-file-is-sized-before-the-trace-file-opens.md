# ADR-1174: A `-T` file is sized before the trace file opens

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4435, AF-0513).
Date: 2026-10-31

## Context

curl 8.22.0 opens and sizes a URL's `-T` file before the transfer, and opens its `--trace` or
`--trace-ascii` file only at the first trace event. With `-T f --trace f` it sizes `f` at its 9
bytes, the trace then truncates it, the first read finds nothing, and curl sends no request and
exits 26 with `client read function EOF fail, only 0/9 of needed bytes read`. Curl opened the
trace file once the first URL parsed (ADR-0046) and the `-T` file only afterwards (ADR-0051), so it
saw an empty file, sent `Content-Length: 0` and exited 0.

## Decision

Just before the run's event output opens, the runner opens the first URL's `-T` file (taken as
written, not standard input) when a trace file is given, notes its size and closes it. When the
transfer then opens the same path and finds it smaller, it uploads it through
`UploadSizedBeforeTraceStream`, which reports the noted size and reads synchronously, as curl's
`fread` does, so the HTTP handler sees the short read before sending the head and sends nothing.
Event output stays opened where ADR-0046 puts it; only the size is taken earlier.

## Consequences

The reproduction of AF-0513 gives curl's exit 26, message and empty request for both trace
options. A file that is not truncated, or a later URL's file, is uploaded as before.
