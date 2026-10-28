# ADR-0964: `--create-dirs` on Windows refuses a directory whose full path is 248 characters or more

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (CGR-188, BL-3717).

## Context

Curl 8.22.0 (Schannel build) makes each `--create-dirs` directory with the C runtime's `mkdir`, which has no
long-path support: a directory whose full path reaches the `CreateDirectory` limit (248 characters, MAX_PATH
less room for an 8.3 file name) fails as `ENOENT`, so curl prints `Error creating directory`, ends with exit 23
and sends nothing. .NET's `Directory.CreateDirectory` handles long paths, so Curl made them all and exited 0
(gap report 2026-10-25, CGO-10 and ENV-14).

## Decision

`PhysicalOutputPaths` takes a `directoryPathLengthLimit`; the production constructor passes 248 on Windows and
none elsewhere. A directory whose `Path.GetFullPath` length is at least the limit is not made and reports
`ENOENT`. Directories that already exist are still passed over first. Off Windows the platform decides, as before.

## Consequences

The message and exit code come from the existing `DirectoryCreationFailure` path. The report's figures were not
re-measured by this lane; the 248 limit is the documented `CreateDirectory` limit.
