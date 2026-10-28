# ADR-0948: A closed standard input fails `@-` data reads and reads as empty elsewhere

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3972 (CGR-110 part 1; parts 2 on in BL-3973 and the tasks split from BL-3639)
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 started with standard input closed (`<&-`) fails its `fread` on `stdin`. Measured
(BL-3639's Notes): `-d @-`, `--data-binary @-`, `--json @-`, `--data-urlencode @-` and
`x@-`, `--url-query @-` and `--variable x@-` exit 26 with
`curl: option <opt>: error encountered when reading a file` and the try-help line, sending
nothing; `-K -`, `-H @-`, `--url @-` and `-T -` read it as empty; `-F f=@-` aborts the
transfer with exit 42 once a server answers. Standard input redirected from the null device
reads as empty everywhere. .NET's `Console.OpenStandardInput()` returns `Stream.Null` for a
closed handle, and tests already pass `Stream.Null` meaning "empty", so Curl could not tell the
two apart and sent the request.

## Decision

- A new public `Curl.Cli.ClosedStandardInputStream` stands for a closed standard input. It
  reads as end of file; `ClosedStandardInputStream.InPlaceOfNull` turns `Stream.Null` into one.
- `DiskDataFileReader.ReadStandardInput` throws `IOException` for it, as `IDataFileReader`
  already documents for unreadable standard input.
- `ConfigFileApplier` (`-K -`) catches that and reads an empty config file.
- Later parts: `Program.cs` swaps `Stream.Null` for the new stream (BL-3973); the `@-` data
  options refuse with exit 26; `-H @-`, `--url @-`, `--ech` and similar read it as empty;
  `-F f=@-` aborts the transfer's read with exit 42.

## Consequences

Nothing observable changes until `Program.cs` makes the swap. Tests that pass `Stream.Null`
keep meaning "empty standard input"; a test of the closed case passes a
`ClosedStandardInputStream`.
