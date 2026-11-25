# ADR-1371: An output file closed by a failed flush fails at close, as curl's fclose does

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5890 (fitness finding STD-209)
- Decided by Claude under Stewart's delegation.

## Context

`-O -J` with a `Content-Disposition` name of `conin$` opens Windows' console input, whose
handle takes no bytes. `DeferredOutputFileStream` flushes after every write and ignores a
failed flush, leaving it for `CompleteAsync` to report, but .NET's `FileStream` closes itself
when that flush fails, so the next write or flush threw `ObjectDisposedException` and Curl
printed `curl: (2) Cannot access a closed file.`. Real curl 8.22.0 (Schannel) exits 23 with
`curl: curl: (23) Failed writing body` and `-w` output for a small body, and with
`Failure writing output to destination` for a body past its stdio buffer (BL-5890 Notes).

## Decision

Once a failed flush has closed the open file (`CanWrite` is false):

- a write smaller than C stdio's 4096-byte buffer succeeds, as `fwrite` holds it in the buffer;
  a larger one fails with an `IOException`, which the handler reports as its write failure;
- `Flush` and `FlushAsync` do nothing;
- `CompleteAsync`'s flush counts as failed, giving curl's close-failure line, exit 23.

## Consequences

The repro exits 23, prints `[conin$ 23]` and no .NET exception text, matching curl 8.22.0. Each
write is judged on its own size rather than on what a real stdio buffer would have held in
total, which is the simplest rule that gives curl's answer for the measured bodies.
