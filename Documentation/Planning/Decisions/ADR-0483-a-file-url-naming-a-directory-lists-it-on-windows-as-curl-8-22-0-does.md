# ADR-0483 — A file:// URL naming a directory lists it on Windows, as curl 8.22.0 does

- **Status:** Accepted
- **Date:** 2026-10-13

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2166
(gap finding GF-0074).

## Context

curl 8.21.0's Windows build could not open a directory, so a `file://` URL naming one
failed with exit 37 there, and Curl matched it: `FileProtocolHandler.ListsDirectories` was
true only off Windows (BL-1401). curl 8.22.0 adds `win32_file_list` to `lib/file.c`
(tag `curl-8_22_0`): when `open` fails on Windows and `stat` says the path is a directory,
`file_do` lists it with `FindFirstFile`, skipping names that start with `.` and writing
each other name then `\n` - the same bytes as the `opendir`/`readdir` branch on Linux and
macOS. Upstream test3203 dropped its `!win32` feature in 8.22.0 to match.

The local reference binary (`/ucrt64/bin/curl`, "curl 8.22.0 (x86_64-w64-mingw32) ...
Schannel", Debug-enabled) was measured on 2026-10-13 and still answers exit 37
(`Could not open file`) for `file://localhost/Z:/temp-bl2166/dir/`, as do the system's
8.21.0 and WinGet's 8.18.0.

## Decision

Curl lists a directory on every platform: `ListsDirectories` is removed and
`DirectoryListerFor` serves a directory through the file system's `IDirectoryLister`
whenever it has one. Windows' `PhysicalFileSystem` already lists with
`Directory.EnumerateFileSystemEntries`, which enumerates with `FindFirstFileEx`, so the
entry order is the order `win32_file_list` sees.

The release's source and its own test case win over the local Debug binary: the binary
contradicts the tagged `lib/file.c`, so it is taken to be built from a tree without
`win32_file_list`, and the gap office's yardstick is the release tarball (ADR-0433).

## Consequences

- `behaviour:test3203` can measure `match` on Windows.
- A directory on a file system that cannot list, or a missing path, is still exit 37.
- If a later measurement of a release-built Windows curl 8.22.0 still answers exit 37,
  this decision is revisited with that measurement.
