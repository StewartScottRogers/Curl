# ADR-0071 — A failed `-z` file lookup off Windows stands in for `stat` and prints its `strerror` text

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0037 left the non-Windows filetime line unmeasured and printed nothing off Windows (BL-288).
curl's `getfiletime` off Windows calls `stat` and, for **any** failure, file not found included,
prints `Warning: Failed to get filetime: <strerror(errno)>` before the two illegal-date lines.
The source is identical in curl 8.18.0 and 8.21.0 (`src/tool_filetime.c`, checked at the
`curl-8_18_0` and `curl-8_21_0` tags).

Measured with curl 8.18.0 (x86_64-pc-linux-gnu, OpenSSL/3.5.5, Ubuntu under WSL) on 2026-09-27,
`curl -z <value> file:///dev/null`, as a non-root user:

| Value | Filetime line |
| --- | --- |
| `missing`, `nodir/x`, `""` | `No such file or directory` |
| `file/x`, `file/` | `Not a directory` |
| `noaccess/x` (directory mode 000) | `Permission denied` |
| a 300-character name | `File name too long` |
| a symbolic-link loop | `Too many levels of symbolic links` |
| a dangling symbolic link | `No such file or directory` |
| a file mode 000, a directory, `dir/` | none: `stat` succeeds and its time is the date |

## Decision

- `DiskDataFileReader.ForPlatform(isWindows)` picks the lookup: on Windows it opens the file as
  before (ADR-0037); elsewhere it stands in for `stat` with `File.GetAttributes` then
  `File.GetLastWriteTimeUtc(path)`, which need no read access and accept a directory.
- .NET reports `ENOTDIR` as `DirectoryNotFoundException`, like `ENOENT`, and ignores a trailing
  separator after a file. The lookup tells both apart itself: a missing directory with a file
  among its ancestors, or a trailing separator after a non-directory, fails as
  `Not a directory`.
- The reason is mapped from the exception: `FileNotFoundException`, `DirectoryNotFoundException`
  and `ArgumentException` (the empty path) read as `No such file or directory`,
  `UnauthorizedAccessException` as `Permission denied`, `PathTooLongException` as
  `File name too long`, and any other `IOException` as the `strerror` text .NET puts before
  ` : '<path>'` in its message.

## Consequences

Every row above matches except the symbolic links: `File.GetAttributes` uses `lstat`, so a
dangling link or a loop reads the link's own time where curl warns (follow-up task).
`-z con` and `-z nul` on Windows still differ: .NET opens both devices with `GENERIC_READ` and
then fails in `GetFileInformationByHandleEx` with `ERROR_INVALID_FUNCTION` for each, so the base
class library's file API gives no signal separating curl's `CreateFile failed: GetLastError
0x00000057` (con) from `GetFileTime failed: GetLastError 0x00000057` (nul); matching them needs
`CreateFileW(FILE_READ_ATTRIBUTES)` and `GetFileTime` through P/Invoke (follow-up task).

## Alternatives considered

- `File.GetUnixFileMode`: the same `lstat`-then-`stat` behaviour, but throws
  `PlatformNotSupportedException` on Windows, so the lookup could not be tested where the
  coverage gate runs.
- `File.OpenHandle` off Windows too: fails for a directory and for a file without read access,
  both of which `stat` reads.
- `Marshal.GetLastPInvokeError` / `strerror` through P/Invoke: leaves the managed file API for
  texts .NET already carries.
