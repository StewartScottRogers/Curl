# ADR-1221: `--etag-save` naming the open `--stderr` file skips the transfer on Windows

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4644 (audit finding AF-0538)

Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's Schannel build on Windows, given `--etag-save f.txt --stderr f.txt URL` (in either
order), cannot open `f.txt` a second time for the ETag while its `stderr` holds it open. It writes
`Warning: Failed creating file for saving etags: "f.txt". Skip this transfer` and
`curl: no transfer performed` into `f.txt`, sends no request and exits 26. Curl opened the file
through `PhysicalFileSystem.OpenForWriteAsync`, which shares writes, so the second open succeeded
and the transfer ran with exit 0.

## Decision

`CurlCommandRunner` keeps the full path of the file the last successful `--stderr` opened. On
Windows, an `--etag-save` file whose full path equals it, compared without regard to case as
Windows paths are, takes the same skip path as a save file that cannot be created. Elsewhere the
transfer goes ahead, as a POSIX `fopen` of a file already open succeeds. The file system's sharing
is left alone, since `%output{file}`, `-D` and the trace files rely on it.

## Consequences

The finding's reproduction exits 26 with no request on Windows, as curl does. Other ways of naming
the same file (a hard link, an 8.3 short name) are not detected; no measurement covers them.
