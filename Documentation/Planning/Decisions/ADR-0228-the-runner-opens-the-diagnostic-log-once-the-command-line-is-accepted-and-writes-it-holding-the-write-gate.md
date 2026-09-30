# ADR-0228 — The runner opens the diagnostic log once the command line is accepted, and writes it holding the write gate

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-919.
Refines ADR-0222 decisions 4 and 8 for `Curl.Console`.

## Context

ADR-0222 says `Curl.Console`'s composition root constructs the diagnostic log. But the log's
level and file come from `--log-level` and `--log-file`, which only exist once
`CurlCommandRunner` has parsed the command line, and its standard-error target only settles once
the parser's `--stderr` options have been carried out. Under `-Z` every write to standard error
goes through the run's `WriteGate`, while `DiagnosticLogWriter` takes its own lock for each line,
so two locks meet on one stream.

## Decision

1. **Where it is opened.** `CurlComposition` gives the runner what the log is built from - the
   `IFileSystem`, the `TimeProvider`, the standard streams - and the runner opens the one log of
   the run (`RunDiagnosticLog`) after an accepted parse, after the parser's warning lines, the
   `--stderr` redirects and the default config file note, and before the first transfer. A refused
   command line opens no log and creates no file. The log is a field of the runner, not a static,
   and the runner closes the `--log-file` when the run ends. `TransferContextFactory.DiagnosticLog`
   puts it on every transfer's context; handlers pass it on as `ConnectTarget.DiagnosticLog`.
2. **Standard error.** Without `--log-file`, `StandardErrorLogTarget` writes each line to the
   runner's standard error as it stands at that write, so the lines follow `--stderr`, `--stderr -`
   included.
3. **Lock order.** Every line is written through `GatedDiagnosticLog`, which takes the run's
   `WriteGate` before `DiagnosticLogWriter` takes its lock. A line is never written inside a line a
   `-Z` transfer is writing, and no thread can hold the log's lock while waiting for the gate, which
   would deadlock against a transfer holding the gate while it logs.
4. **An unopenable `--log-file`** writes `Warning: Failed to open the --log-file <path>` even under
   `-s`: the user asked for the log in the same command, as ADR-0222 decision 4 reasons for the log
   lines themselves.
5. **What the runner logs** (component `cli` or `runner`): `info` the accepted command line's group
   and URL count; `verbose` each file the parse tried to read (`.curlrc` candidates, `-K` files and
   the `@file` values read while parsing), saying whether it was read - the parser in
   `Curl.Cli.UnitLibrary` reports no config file list, so the runner records the paths its
   `IDataFileReader` is asked for; `warning` each `Warning: ` line printed, the parser's under `cli`
   and the runner's under `runner`; `info` each transfer's start, with scheme, host, `-X` method,
   output target and which of `-v`, `--trace` and `-s` are on, and `credentials given` in place of any
   credential; `error` a failing exit code with its `CurlExitCode` name; `info` each transfer's end
   with exit code, bytes transferred and elapsed milliseconds; `verbose` the `-Z` scheduler starting
   and finishing each transfer.

## Consequences

- The composition root stays a set of constructor calls, and `--log-level none` still constructs no
  writer and opens no file.
- A `-Z` transfer that logs waits for the gate as a transfer that writes to standard error does.
- Parser warnings are logged just after the log opens, not as they are printed, because the log
  does not exist while they are printed.

## Alternatives considered

- **Build the log in `CurlComposition`.** Lost: the level is not known there, and parsing twice to
  learn it would read the config files twice.
- **Let `DiagnosticLogWriter` write straight to the gated stream.** Lost: the log's lock taken
  before the gate deadlocks under `-Z`.
- **Hide the `--log-file` warning under `-s`.** Lost: it is about output the user asked for.
