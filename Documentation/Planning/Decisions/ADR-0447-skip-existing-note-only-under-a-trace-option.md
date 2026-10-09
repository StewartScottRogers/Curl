# ADR-0447: The --skip-existing note stays behind -v or a trace option

- Status: Accepted
- Date: 2026-10-08
- Decided by Claude under Stewart's delegation
- Task: BL-1806 (gap items `behaviour:test994`, `behaviour:test996`, `behaviour:test1491`, GF-0013)

## Context

GF-0013 found that Curl prints nothing when `--skip-existing` skips a transfer, while upstream
tests 994, 996 and 1491 expect `Note: skips transfer, "<file>" exists locally` on standard error,
and suggested writing the note always, even under `-s`.

curl 8.21.0 writes the note through `notef`, which prints only when a trace type is set (`-v` or a
`--trace` option). Measured 2026-10-08 with the mingw Schannel build: `curl -s URL -o there
--skip-existing` with `there` present prints nothing and exits 0; adding `--trace-ascii -
--trace-time` prints the note (CRLF-ended) to standard error and nothing to standard output.
Upstream's test harness adds `--trace-ascii` and `--trace-time` to every curl it runs, which is why
the three tests' `<verify><stderr>` show the note. Curl already prints it under any trace option
(BL-493).

## Decision

Curl keeps curl 8.21.0's behaviour: the note is written only under `-v` or a `--trace` option, `-s`
or not. It is not written on a plain or silent command line, as the finding suggested, because
real curl does not. `CurlCommandRunnerSkipExistingTests` pins the harness's command line
(`-s --trace-ascii - --trace-time`).

## Consequences

- The gap closes when the gap office's upstream-case runner gives Curl the trace options
  upstream's harness gives curl; that runner lives under `Gap/`, which only an interactive session
  may change (ADR-0433).
- A script that runs `--skip-existing` without tracing sees the same silence from both binaries.
