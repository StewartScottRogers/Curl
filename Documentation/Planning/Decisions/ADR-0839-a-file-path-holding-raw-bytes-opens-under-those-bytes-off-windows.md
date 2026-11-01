# ADR-0839 — A file path holding bytes that are not UTF-8 opens under those bytes off Windows

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3899, CGR-114).

## Context

CGR-114 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`) measured curl 8.18.0 (OpenSSL,
Linux): with a file named by the raw bytes `63 61 66 E9 2E 74 78 74`,
`curl -sS file://$PWD/caf%E9.txt` prints it and exits 0, because curl hands the decoded bytes to
`open()` unchanged. Curl exits 37: `FileUrlPath` decodes escapes as UTF-8 into a .NET string, and
.NET's file APIs encode a name as UTF-8, so no string they take names that file.
`Curl.Original.Protocol.File.UnitLibrary` reaches the disk only through `IFileSystem`, which takes a `string`.

## Decision

1. `Curl.Protocol.Abstractions.UnitLibrary`'s `RawBytePath` is the text form of a path that may hold
   bytes that are not valid UTF-8: `FromBytes` reads UTF-8 and keeps each byte `b` outside a valid
   sequence as the lone surrogate U+DC00 + `b` (U+DC80 to U+DCFF), and `ToBytes` gives each one back
   its byte. This is the scheme `Curl.Cli`'s `InvalidUtf8ByteEscape` uses for arguments (ADR-0909);
   it is written again in Abstractions because the file protocol references nothing else, and the
   two are the same mapping. A path of valid UTF-8 is unchanged by it.
2. `Curl.Core`'s `PhysicalFileSystem.OpenForReadAsync`, off Windows, opens a path holding such a
   surrogate with the runtime's non-variadic `SystemNative_Open` shim (`O_RDONLY | O_CLOEXEC`) on its
   NUL-terminated raw bytes, through `LibraryImport` (AOT-safe, as `Curl.Console`'s `RawByteFileName`
   does for `-o`, ADR-0920), and wraps the descriptor in a `FileStream`. A failed open is the status
   of its `errno` (`ENOENT` is `NotFound`, `EACCES` and `EPERM` `AccessDenied`, `ENOTDIR`
   `NotADirectory`, `EISDIR` `IsDirectory`, any other `IoError`), so a missing name still fails as a
   missing file does. Every other path, and every path on Windows (whose file APIs take UTF-16 and
   whose curl build has no raw bytes to pass), opens as before.
3. Only the read open takes raw bytes here. Producing a `RawBytePath` from `file://` escapes is
   `FileUrlPath`'s change (BL-3643); a write open or a directory listing under raw bytes waits for a
   case that needs it.

## Consequences

The native adapter is excluded from coverage (ADR-0083); the routing, the `errno` mapping and the
`FileStream` wrapping are covered on every platform through an injected opener, and the open of a
real raw-byte file is tested on Linux and macOS only.
