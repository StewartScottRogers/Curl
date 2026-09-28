# ADR-0097 — A `-F` file that cannot seek declares the length curl's `stat` gives it

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; recorded
  by BL-401, 2026-09-27).

## Context

`MultipartFormBodyBuilder` sent a file part whose stream cannot seek (a pipe or a device)
with no length, so the body went chunked. BL-359 noticed that curl 8.21.0 did not.

BL-401 measured the reference build (the mingw Schannel build, ADR-0009) with
`Record-CurlExchange.ps1`. The commands and bytes are in BL-401's `Notes`:

| File part | `Content-Length` | Body sent | Exit |
| --- | --- | --- | --- |
| a one-instance named pipe delivering 5 bytes | a 1-byte file's length | cut off at that length, without the closing `--` CRLF | 0 |
| the same pipe delivering 500 bytes | a 1-byte file's length | cut off the same way | 0 |
| the same pipe delivering nothing | a 1-byte file's length | one byte short of it, then curl waits for the server | 0 once the server answers |
| a three-instance pipe delivering 3 bytes | a 3-byte file's length | whole | 0 |
| `NUL` | a 0-byte file's length | whole | 0 |

libcurl 8.21.0's `curl_mime_filedata` (`lib/mime.c`) gives a part the size `stat` reports
when `S_ISREG` holds and an unknown size, so a chunked body, otherwise. On Windows `stat` is
the C runtime's `_wstati64`, which calls a named pipe and `NUL` regular files; a named pipe's
size is what its entry in `\\.\pipe\` reports, the number of instances its server created.
.NET's directory enumeration reads the same entry: `FileSystemEntry.Length` of a pipe is its
instance count (measured: 1 and 3).

## Decision

1. `UnseekableFileLength` (`Curl.Core.UnitLibrary/Multipart`) says what length a file that
   cannot seek declares. On Windows, `AsWindowsStatReportsIt`: a local named pipe
   (`\\.\pipe\name` or `\\?\pipe\name`, either slash) declares its instance count, read from
   `\\.\pipe\`, and anything else (`NUL`, `CON`, a remote pipe) declares zero. Elsewhere,
   `Unknown`: no length, so the body stays chunked, as libcurl sends a FIFO or a character
   device there.
2. `MultipartFormBodyBuilder` takes that rule as an optional constructor argument, defaulting
   to `UnseekableFileLength.ForPlatform(OperatingSystem.IsWindows())`, so `Curl.Console`
   gets the platform's rule without a change.
3. The body is not cut off in Core. The HTTP sender already stops at a `StreamBody`'s
   `Length`, which is where curl stops.
4. **Not matched:** a pipe that delivers fewer bytes than it declared. curl sends what it has
   and waits for the server; our HTTP sender fails the transfer with exit 26 once the stream
   ends early. Waiting on a server that is itself waiting for the missing bytes only ends in a
   timeout, and curl's own result depends on when the pipe closes (a close during the read
   gave exit 26, `read error getting mime data`, in an earlier run).

## Consequences

- `curl -F f=@\\.\pipe\name` sends curl's first request byte for byte on Windows, including
  the truncated body.
- The Linux and macOS rule comes from libcurl's source, not from a measurement; BL-402 is
  where the OpenSSL build is measured.
- `/dev/null` on Linux and macOS opens seekable with length zero (`PhysicalFileSystem`), so
  it declares `Content-Length` where libcurl, seeing a character device, would send it
  chunked. Not changed here.
