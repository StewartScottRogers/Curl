# ADR-0090 — The `-z` `stat` lookup off Windows follows a final symbolic link

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

ADR-0071 stands in for `stat` with `File.GetAttributes`, which uses `lstat`, then
`File.GetLastWriteTimeUtc(path)`, which falls back to the link's own `lstat` time when `stat`
fails. A dangling link or a link loop therefore read the link's own time, where curl warns.

Measured with curl 8.18.0 (OpenSSL/3.5.5, Ubuntu under WSL) on 2026-09-27, `getfiletime`
identical in 8.21.0:

| Value | Filetime line |
| --- | --- |
| `ln -s nothing dangling`, `-z dangling` | `Warning: Failed to get filetime: No such file or directory` |
| `ln -s loop1 loop2; ln -s loop2 loop1`, `-z loop1` | `Warning: Failed to get filetime: Too many levels of symbolic links` |
| `ln -s f goodlink`, `-z goodlink` | none: the target's time is the date |

## Decision

- After `File.GetAttributes` succeeds, the lookup resolves the path's final link target with
  `File.ResolveLinkTarget(Path.GetFullPath(path), returnFinalTarget: true)`. The full path is
  passed because a relative path resolved relative targets against `/` in a probe.
- A resolved target that does not exist fails as `stat` does: `Not a directory` when a file
  stands among its ancestors, otherwise `No such file or directory`. Existence is asked with
  `Path.Exists`, because `File.ResolveLinkTarget` returns a `FileInfo` even for a directory.
- An `IOException` of exactly that type from the resolution is the loop .NET detects after
  following its maximum number of links, and reads as `Too many levels of symbolic links`;
  its subclasses (file or directory not found, name too long) and `UnauthorizedAccessException`
  keep the reasons ADR-0071 maps them to.
- An existing target's time is read with `File.GetLastWriteTimeUtc(target)`; a path that is not
  a link reads its own time as before.
- `DiskDataFileReader.ForStatFollowingLinksWith(resolveFinalLinkTarget)` exposes the resolver as
  a seam: creating a symbolic link on Windows needs Developer Mode, and the coverage gate runs
  on Windows. Tests on Linux and macOS create real links.

## Consequences

`-z` with a dangling link, a link loop or a link to an existing file or directory matches curl
off Windows. Any other plain `IOException` during resolution (for example `EIO` from
`readlink`) would also read as the loop text; curl would print that error's `strerror`.

## Alternatives considered

- `stat` through P/Invoke: exact `errno`, but leaves the managed file API and needs a per-OS
  `struct stat` layout under native AOT.
- `File.GetUnixFileMode`: the same `lstat` behaviour, and unsupported on Windows.
