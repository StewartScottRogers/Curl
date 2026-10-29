# ADR-0920: Output file names holding invalid UTF-8 argument bytes open under their raw bytes

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3853, CGR-016)
- Relates to: [ADR-0909](ADR-0909-linux-and-macos-read-argv-raw-bytes-with-surrogate-escapes.md) (BL-3849's lossless argument decoding), [ADR-0083](ADR-0083-thin-socket-adapters-are-measured-by-the-integration-run.md)

## Context

Since BL-3849, Linux and macOS read each argument's raw bytes and turn every byte that is not part of
valid UTF-8 into the lone surrogate U+DC00 + byte (`InvalidUtf8ByteEscape`). .NET's file APIs encode a
path as UTF-8 with such a surrogate as EF BF BD, so `-o $'\xff.txt'` created `EF BF BD .txt`. Measured on
2026-10-26 with `Record-CurlExchange.ps1 -Curl wsl.exe` against curl 8.18.0 (OpenSSL, Ubuntu):

| Command | curl 8.18.0 |
| --- | --- |
| `curl -s -o $'\xff.txt' URL` | exit 0, file `FF 2E 74 78 74` holding the body |
| `curl -s --output-dir $'\xfe' -o a.txt -D $'\xfc.h' URL URL` | `\xfe/a.txt` and `\xfc.h`, raw bytes |
| `curl -s --create-dirs -o $'\xfd/b.txt' URL` | directory `\xfd`, raw bytes |

curl never re-encodes argv: every name is the bytes typed.

## Decision

`DeferredOutputFileStream` opens its file through `RawByteFileName.OpenForWriteAsync`. Off Windows, a
name holding a surrogate from U+DC80 to U+DCFF is encoded with `InvalidUtf8ByteEscape.Encoding` (each such
surrogate back to its byte) and opened with the runtime's own `SystemNative_Open` from `libSystem.Native`,
the non-variadic shim every .NET runtime on Linux and macOS ships and Native AOT links in; libc's `open` is
variadic, which a P/Invoke cannot call correctly on Apple silicon. The descriptor is wrapped in a
`SafeFileHandle` and a `FileStream`. The flags are the runtime's PAL values (`O_WRONLY | O_CLOEXEC | O_CREAT`
plus `O_TRUNC` or `O_EXCL`; append opens without truncating and seeks to the end, as `FileMode.Append`
does), the permissions are `--create-file-mode`'s, and `errno` maps to the `FileAccessStatus` the warning
lines need. Every other name, and every name on Windows (whose arguments are never escaped), opens through
the `IFileSystem` as before. Since `--output-dir` is put in front of the `-o` name before the stream gets
it, `--output-dir` with such bytes is covered too.

`-D` files and the directories `--create-dirs` makes do not go through this stream; they are BL-3868's.

## Consequences

- `-o`, `-O` and `-J` files, `--no-clobber`'s numbered names and `--output-dir` keep raw bytes, as curl does.
- The native open is an `[ExcludeFromCodeCoverage]` adapter under ADR-0083; the decision, the bytes, the
  flags and the `errno` mapping are unit-tested on every platform (`Cgr016OutputFileTests`). It was
  checked on Linux by running a linux-x64 build of Curl in WSL (BL-3853 Notes).
