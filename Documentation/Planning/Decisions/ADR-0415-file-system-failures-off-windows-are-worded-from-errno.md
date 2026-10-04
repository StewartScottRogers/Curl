# ADR-0415 — File-system failures off Windows are worded from errno

- Status: Accepted
- Date: 2026-10-04
- Task: BL-1433
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0's POSIX build words a failed `-R` stamp as
`Warning: Failed to set filetime <seconds> on '<file>': <strerror>` (`src/tool_filetime.c`
lines 128-148) and a failed `--create-dirs` `mkdir` by its errno (`src/tool_dirhie.c`,
`show_dir_errno`), ignoring `EACCES` and `EEXIST` so traversal goes on. Curl printed the
Windows `CreateFile failed: GetLastError` line and `Error creating directory` everywhere.
.NET's `File.SetLastWriteTimeUtc` reports a Unix failure as an exception whose Win32-style
code is not an errno (`UnauthorizedAccessException` reads as 5, which is `EIO`), and
`Directory.CreateDirectory` reports errno only through the exception type or, for errno it has
no type for, its `HResult`.

## Decision

1. Off Windows `PhysicalFileSystem.TrySetLastWriteUnixSeconds` always sets the time with
   `NativeFileTimeSetter`'s `utimes`, so the error code is a real errno; Windows is unchanged.
2. `CRuntimeErrorNumbers` holds each platform's numbers (glibc, macOS, the Windows C runtime)
   and `strerror` texts for the errnos the messages need; an unknown errno reads
   `Unknown error <n>` (glibc) or `Unknown error: <n>` (macOS). The runner picks the table from
   `runsOnWindows` and `OperatingSystem.IsMacOS()`.
3. `IOutputPaths.TryCreateDirectory` gives the errno. `PhysicalOutputPaths` reads it from the
   exception: `UnauthorizedAccessException` is `EACCES` (.NET folds `EPERM` into it, so an
   `EPERM` directory is passed over rather than reported; the later open still fails),
   `PathTooLongException` `ENAMETOOLONG`, a missing parent `ENOENT`, a Win32 code as `_dosmaperr`
   maps disk-full and already-exists, and otherwise the raw `HResult`.
4. On Windows the runner keeps the measured `CreateFile failed` line: `File.SetLastWriteTimeUtc`
   does not say whether the open or the stamp failed. `RemoteTimeFailureWarning.ForWindowsStamp`
   holds the `SetFileTime failed` form for when the setter can tell them apart.

## Consequences

Linux and macOS print curl's own texts for these failures. The POSIX strings come from
upstream's source and the C libraries' `strerror` tables, not a measurement on Linux or macOS;
CI on those platforms pins them only through injected tables.
