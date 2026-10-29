# ADR-0978: Off Windows, `-w` text and the runner's standard error lines write escaped bytes raw

Status: Accepted. Decided by Claude under Stewart's delegation (BL-4032).

## Context

Off Windows an output name holding a byte that is not valid UTF-8 (`-o $'\xff.txt'`, or a `-J`
name, ADR-0977) carries that byte as its `InvalidUtf8ByteEscape` surrogate (U+DC00 + byte,
ADR-0920). `RawByteFileName` opens the file under the raw bytes, but `%{filename_effective}` and
the `Warning: Failed to open the file <name>: File exists` line were encoded as plain UTF-8, so
the surrogate came out as U+FFFD (EF BF BD). curl 8.18.0 OpenSSL on Linux, measured through WSL
on 2026-10-28, prints the raw bytes in both: `-o $'\xff.txt' -w "%{filename_effective}"` prints
FF 2E 74 78 74, and with `--no-clobber` and every name taken standard error gets
`Warning: Failed to open the file \xff.txt: File exists`.

## Decision

Off Windows `CurlCommandRunner` gives its `-w` renderer, and the default encoding of its own
standard error lines (`WriteErrorLineAsync`, `WriteErrorLinesAsync`, `WriteErrorPiecesAsync`),
`InvalidUtf8ByteEscape.Encoding`: UTF-8 for every character except an escape surrogate, which is
written as its byte. Text without one is written exactly as before. Windows keeps UTF-8 for
standard error and the ANSI code page for `-w`, unchanged.

## Consequences

Any command-line text holding an escaped byte that reaches `-w` or a runner warning now prints the
byte curl prints. Lines written through another encoding (the command-line echo encoding, the
retried attempt's open warning in `OutputFileOpenFailureTransferEvents`) are unchanged.
