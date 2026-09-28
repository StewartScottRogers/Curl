# ADR-0088 — The Windows `-z` file lookup calls `CreateFile` and `GetFileTime` as curl does

- **Status:** Accepted; replaces the Windows lookup of [ADR-0037](ADR-0037-a-z-value-that-is-not-a-date-is-read-as-a-file-through-idatafilereader.md)
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0037 looked a `-z` file up on Windows with `File.OpenHandle` (`GENERIC_READ`) and
`File.GetLastWriteTimeUtc(handle)`. Both DOS devices open that way and then fail with
`ERROR_INVALID_FUNCTION`, so `-z con` and `-z nul` printed `CreateFile failed: GetLastError
0x00000001` where curl 8.21.0 (Schannel build) prints `CreateFile failed: GetLastError 0x00000057`
for `con` and `GetFileTime failed: GetLastError 0x00000057` for `nul` (BL-378). ADR-0071 found
that the base class library's file API gives no signal telling the two apart.

curl opens the file with `CreateFile(name, FILE_READ_ATTRIBUTES, FILE_SHARE_READ |
FILE_SHARE_WRITE | FILE_SHARE_DELETE, NULL, OPEN_EXISTING, 0, NULL)` and then calls
`GetFileTime` (`src/tool_filetime.c`, `curl-8_21_0` tag).

## Decision

- `WindowsFileTimeReader` in `Curl.Cli.UnitLibrary` makes the same two calls through
  `[DllImport]` of kernel32 `CreateFileW` and `GetFileTime`, with `SetLastError` and the
  `System32` search path, as `Curl.Console`'s `GetStdHandle` import already does. It stays behind
  `DiskDataFileReader`'s `readLastWriteTimeUtc` seam; `ForPlatform(isWindows: true)` picks it.
- A failed call throws `FileTimeLookupException`, an `IOException` naming the call
  (`CreateFile` or `GetFileTime`) and carrying its `GetLastError` code, also as the exception's
  `HResult`. The warning names that call; any other exception still reads as `CreateFile`.
- `[DllImport]` rather than `[LibraryImport]`: the source-generated form needs
  `AllowUnsafeBlocks` in the project, and native AOT compiles the marshalling for these
  signatures (a string and a `SafeFileHandle`) ahead of time either way.

## Consequences

The same calls fail the same way, so every row of ADR-0037's measured table matches, `con` and
`nul` included; `DiskDataFileReaderTests` asserts each against the real disk and devices. A file
the process may read attributes of but not data now has a time, as it does for curl. The lookup
is Windows only; `ForPlatform(isWindows: false)` keeps ADR-0071's `stat` stand-in.

## Alternatives considered

- `File.GetAttributes` before the open: fails for `con` with 0x80070057 but gives nothing for
  `nul`, and a directory must still fail as 0x00000005 in the open (BL-378 context).
- Recognising the device names: `con`, `nul`, `C:\x\con.txt` and the rest are Windows' rules to
  apply, not ours; the real calls apply them.
