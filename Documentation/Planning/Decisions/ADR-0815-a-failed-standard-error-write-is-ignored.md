# ADR-0815: A failed write to standard error is ignored

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3537
- Decided by Claude under Stewart's delegation.
- Keeps ADR-0766 as it is.

## Context

Third-party gap report issue CGR-008: on Linux, with standard error on a full device
(`2>/dev/full`), every write to it fails with ENOSPC, which .NET's console stream throws as an
`IOException`. The first `curl: (7) ...` line, progress meter line or `-v` line threw out of the
runner, `UnhandledExceptionGuard` (ADR-0766) caught it and wrote its own `curl: (2)` line to the
same full stream, and that second exception escaped `Main`; the runtime then aborted (SIGABRT,
exit 134). curl 8.18.0 (OpenSSL, WSL) ignores the failed `fputs` and `fflush` on `stderr`: measured
on 2026-10-26, `-sS` to a closed port exits 7, a transfer with the progress meter 0, `-v` 0,
`--badopt` 2 and `-w '%{stderr}x'` 0, all with `2>/dev/full`.

## Decision

`Program.Main` wraps the process's standard error in `StandardErrorFailureIgnoringStream`, which
passes every write and flush through and ignores an `IOException` from either. Every standard
error line - the runner's, the progress meter's, `-v`'s, `-w %{stderr}`'s and the guard's - goes
through it, so a standard error that cannot be written never changes the exit code or the bytes
anywhere else. Standard output keeps its own failure handling (`StandardOutputFailureDeferringStream`).

## Consequences

- The five repros of CGR-008 give curl's exit codes on Linux.
- A closed standard error (`2>&-`) still aborts when a line is written to it, as it did before
  this change; that is a different failure (the runtime reuses descriptor 2) and is its own task.
