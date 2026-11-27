# ADR-1425: The trace file opens at the first trace event

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation
- Task: BL-6023 (fitness finding STD-226)

## Context

`curl -sS --trace tr.txt -F f=@nofile.txt http://...` exits 26 and leaves no `tr.txt`; Curl
left an empty one, and would also have emptied a `tr.txt` that was already there. curl's tool
opens the trace file inside its debug callback, so a run that fails before its first event never
touches it. Measured on curl 8.21.0 (Schannel): no file for `-F` or `-T` of a missing file or a
bad glob, and an existing file is kept; a file for an unsupported protocol or a host that does
not resolve, which report events.

## Decision

- `TraceFileOpenedOnFirstWriteStream` opens and truncates the named file on its first write. A
  file that cannot be opened still sends the dump to standard error, with no warning (ADR-0046).
- Trace events are reported synchronously (`ITransferEvents` returns nothing), so the open cannot
  be awaited. The stream requires `IFileSystem.OpenForWriteAsync` to hand back a completed task,
  which `PhysicalFileSystem` always does, reads its result without blocking, and throws
  `InvalidOperationException` otherwise.
- A trace file that is also a `-T` value is still opened before the transfer. The runner takes the
  upload's size first and expects it only when the opened file is smaller (ADR-1175), so this keeps
  `-T f --trace f` failing with exit 26 as curl's does.

## Alternatives considered

- Adding a synchronous open to `IFileSystem`: every implementation and fake would change for one
  caller.
- Deleting an empty trace file at the end of the run: an existing file would still be truncated.
- Opening lazily for `-T f --trace f` too: needs the runner to wrap every upload sized before the
  trace, not only a shrunken one. The one difference left is that a run naming one file for both
  that fails at setup empties it, where curl keeps it.
