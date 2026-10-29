# ADR-0943: `-D` files and `--create-dirs` directories holding invalid UTF-8 argument bytes use their raw bytes

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3868, CGR-016)
- Relates to: [ADR-0920](ADR-0920-output-file-names-with-invalid-utf-8-bytes-open-under-their-raw-bytes.md) (the `-o`, `-O` and `-J` files), [ADR-0083](ADR-0083-thin-socket-adapters-are-measured-by-the-integration-run.md)

## Context

ADR-0920 opened `-o`, `-O` and `-J` files under their raw bytes off Windows, but the `-D` file still
opened through `IFileSystem.OpenForWriteAsync` and `--create-dirs` made directories through
`Directory.CreateDirectory`, both of which write an escaped byte (U+DC80..U+DCFF) as EF BF BD.
Measured on 2026-10-28 in WSL (Ubuntu) against curl 8.18.0 (OpenSSL), in an empty directory:

| Command | curl 8.18.0 | Curl before | Curl after |
| --- | --- | --- | --- |
| `curl -s -D $'\xfc.h' -o a.txt file:///etc/hostname` | `FC 2E 68`, exit 0 | `EF BF BD 2E 68` | `FC 2E 68`, exit 0 |
| `curl -s --create-dirs -o $'\xfd/b.txt' file:///etc/hostname` | directory `FD`, file `FD/b.txt`, exit 0 | directory `EF BF BD` | directory `FD`, file `FD/b.txt`, exit 0 |

## Decision

- `CurlCommandRunner` opens a `-D` file through `RawByteFileName.OpenForWriteAsync`, as
  `DeferredOutputFileStream` opens `-o`: a name holding an escaped byte off Windows opens with the
  runtime's `SystemNative_Open`, every other name through the `IFileSystem`.
- `PhysicalOutputPaths` takes an optional raw-byte directory maker; off Windows it is
  `RawByteFileName.MakeDirectoryUnderRawBytes`, the runtime's non-variadic `SystemNative_MkDir`, used for a
  directory name holding an escaped byte. Its `errno` is the reported error number, and `EEXIST` counts
  as made, as an existing directory does for every other name. The mode is 0777 before the umask, the
  mode `Directory.CreateDirectory` gives every other name, so both kinds of name get the same permissions.
- The P/Invoke adapter is excluded from coverage (ADR-0083); the choice of path and the errno handling
  are unit tested with a fake maker (`Cgr016HeaderFileAndDirectoryTests`).
- `--skip-existing`'s `Exists` and `--remove-on-error`'s delete still go through .NET's file APIs; they are
  left to a follow-up task, which measures curl first.

## Consequences

Every file and directory a command line names now carries the same bytes as curl 8.18.0's on Linux and
macOS. Windows and every valid UTF-8 name are unchanged.
