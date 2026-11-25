# ADR-1385: The cookie jar, HSTS and alt-svc files are replaced by rename on Windows too

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5933
- Supersedes the Windows decision of ADR-1368.
- Decided by Claude under Stewart's delegation.

## Context

ADR-1368 left Windows writing the `-c`, `--hsts` and `--alt-svc` files in place, unmeasured.
Git for Windows' curl 8.21.0 and 8.22.0 and Windows' own curl 8.23.0 (all Schannel) were
measured on NTFS, each saving each of the three files after a successful `http://` transfer
from a loopback server, comparing the file ID, attributes and content before and after. All
three builds and all three files behaved alike; `curl -sS` printed nothing and exited 0 in
every case.

| Case, the same for all three files | What curl left |
| --- | --- |
| Existing file | A new file (new file ID, `Archive`) with the saved content |
| Read-only file | The old file, content and attribute unchanged, after about 1 s; no temporary file |
| Hard-linked file | A new file at the path; the other name keeps the old file ID and content |
| Held open by another process, sharing nothing, read, read and write, or read, write and delete | The old file unchanged, after about 1 s; no temporary file |
| Held open and released after about 0.5 s | Replaced |
| Hidden file | Replaced; the new file is not hidden |
| Symbolic link | The link replaced by a regular file; its target unchanged |
| Missing file | Created |

A read-only file, a `NUL` path or a directory draws no `-v` message either (measured with a
refused connection). A named pipe received no bytes. A `file://` transfer saved none of the
three files; that is about when they are saved, not how, and is outside this decision.

This is libcurl's `Curl_fopen`, which `stat`s the path, writes a regular file's replacement
to a temporary file, and `curlx_rename`s it over the path, retrying `MoveFileEx` for 1 s.

## Decision

- On Windows a `FileWriteMode.Replace` open of an existing file (`File.Exists`, false for a
  directory and for a device such as `NUL`) never opens it: `PhysicalFileSystem` writes
  `<random>.tmp` beside it and `ReplacementFileStream` renames that over the path.
- A refused rename is retried a millisecond apart for 1 s, timed by an injected
  `TimeProvider`, then the temporary file is deleted and the failure is not reported. The
  retry runs only in `DisposeAsync`, which the three savers use; a synchronous `Dispose`
  tries once rather than block a thread.
- A path naming nothing yet is written in place, as curl does.
- Off Windows nothing changes: ADR-1368 stands there, without a retry.

## Consequences

A saved file loses the old one's hidden attribute, access control list and hard links, as with
curl. Saving over a file that stays read-only or open takes 1 s longer, as with curl.
