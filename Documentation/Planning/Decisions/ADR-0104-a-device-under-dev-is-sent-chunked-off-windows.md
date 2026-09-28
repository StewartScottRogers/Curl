# ADR-0104 — A `-F` device under `/dev/` is sent chunked off Windows

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-445.

## Context

ADR-0097 found that libcurl's `curl_mime_filedata` gives a file part the `stat` size only
when `S_ISREG` holds, and an unknown size (a chunked body) otherwise. On Linux and macOS
`/dev/null` accepts `lseek`, so `PhysicalFileSystem` opens it seekable with length 0 and
`MultipartFormBodyBuilder` declared `Content-Length` for it. BL-445 measured the OpenSSL
build (curl 8.18.0, WSL Ubuntu): `curl -F f=@/dev/null` sends
`Transfer-Encoding: chunked` and `Expect: 100-continue`.

The base class library has no way to ask a file's type: `FileAttributes` on Unix carries
no device flag, `File.GetUnixFileMode` masks off the type bits, and .NET 10 has no public
`stat`. A P/Invoke of `stat` would depend on a `struct stat` layout that differs between
Linux architectures and macOS.

## Decision

- **A seekable file declares what `SeekableFileLength` says.** The builder takes a
  `Func<string, long, long?>`; the default is `SeekableFileLength.ForPlatform`.
- **On Windows the opened length stands**: `_wstati64` calls every seekable file regular.
- **Off Windows a device is known by its path**: one starting `/dev/` declares no length,
  so the body goes chunked, except `/dev/shm/` (tmpfs, regular files), `/dev/fd/` and
  `/dev/stdin`, `/dev/stdout`, `/dev/stderr`, which name whatever the descriptor was opened
  on - a regular file when it seeks, as a redirect gives it.
- **The path is taken as given**: no symbolic link is resolved and a relative path is not
  made absolute.

## Consequences

- `-F f=@/dev/null`, `/dev/zero`, `/dev/urandom` and block devices go chunked on Linux and
  macOS, as curl sends them.
- A relative path into `/dev` (`../dev/null`), a symbolic link elsewhere that points at a
  device, or `/dev/fd/N` opened on a device still declares its length. Should one matter,
  the fix is a real file-type check, which needs a `stat` the base class library does not
  offer.
