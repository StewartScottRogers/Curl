# ADR-1358: The cookie jar, HSTS and alt-svc files are replaced by rename off Windows

- Status: Accepted
- Date: 2026-11-24
- Task: BL-5888 (fitness finding STD-207)
- Decided by Claude under Stewart's delegation.

## Context

Curl rewrote the `-c` cookie jar, the `--hsts` cache and the `--alt-svc` cache in place, so
off Windows an old world-writable mode stayed and a symbolic link was written through. The
fitness review measured curl 8.18.0 (OpenSSL, Ubuntu): each file gets a new inode and mode
0644 under umask 022, and a symlinked jar becomes a regular file whose old target receives no
cookies. That is libcurl's `Curl_fopen`: open the path for writing, and when it is a regular
file write a temporary file beside it, created with the old mode and `0600`, and rename it
over the path. `-o` and `--etag-save` write in place in both.

## Decision

- `FileWriteMode.Replace` asks an `IFileSystem` for that behaviour; the three savers use it
  through `FileSystemReplacement.TryReplaceFileAsync`. A file system that does not tell it
  apart truncates, so test stand-ins need no change.
- `PhysicalFileSystem` does it off Windows: it opens the path as a truncating open does
  (creating a dangling link's target, and truncating a link's target, as curl's `fopen`
  does), asks the runtime's `fstat` shim whether that is a regular file, and if so writes
  `<random>.tmp` in the same directory with the old mode and owner read and write, narrowed by
  the umask. Closing renames it over the path, or deletes it when a write failed. A device or
  pipe (`/dev/null`, `/dev/stdout`) is written in place and never renamed over.
- A file that cannot be replaced is not reported, as curl does not report it. This also
  stops a full disk while saving the HSTS or alt-svc file escaping as an exception.
- Windows keeps writing in place: the finding and its measurement are of the Linux build, and
  the Windows build has not been measured.

## Consequences

The owner and group of a replaced file become the saving process's. The Linux and macOS
behaviour is pinned by tests that exclude Windows, so it is first run by CI there.
