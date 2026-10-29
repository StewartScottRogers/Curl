# ADR-0962: A -T directory off Windows uploads an empty stream of unknown size

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3895 (CGR-069, split from BL-3598)
- Decided by Claude under Stewart's delegation.

## Context

`curl -sS -T sub http://host/a`, where `sub` is a directory, measured with `Record-CurlExchange.ps1`
against curl 8.18.0 (OpenSSL) in WSL Ubuntu, the only Linux build on the lane machine: exit 0, nothing
on standard output or standard error, and the request
`PUT /a HTTP/1.1`, `Transfer-Encoding: chunked`, `Expect: 100-continue`, then only the last chunk
`0\r\n\r\n`. curl's `open(2)` of a directory succeeds on Linux, `fstat` finds no regular file so the
upload size stays unknown, and `tool_read_cb` turns `read(2)`'s `EISDIR` into end of file. The Windows
build cannot open a directory and prints `curl: cannot open 'sub'` with exit 26, which Curl already did
on every platform.

## Decision

When the runner is not the Windows build (`runsOnWindows` false) and opening the `-T` path fails with
`FileAccessStatus.IsDirectory`, `CurlCommandRunner` uploads an empty, non-seekable stream with no known
size, the same `StandardInputUploadStream` a `-T -` upload goes through, so every protocol treats it as
it treats an empty standard input (HTTP sends it chunked). The `-Z` first-batch open check counts such a
directory as opened. `PhysicalFileSystem.OpenForReadAsync` keeps reporting the directory, since the
`file://` handler relies on that status.

## Consequences

Curl's request bytes for `-T -` with empty standard input match the Linux capture (the User-Agent
version aside). The Windows build is unchanged.
