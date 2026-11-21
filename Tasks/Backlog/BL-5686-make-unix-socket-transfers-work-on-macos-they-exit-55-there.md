---
id: BL-5686
title: Make --unix-socket transfers work on macOS (they exit 55 there, pass on Windows and Linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UnixSocketAddress.cs, Curl.Networking.UnitLibrary/TcpDialer.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5686 — Make --unix-socket transfers work on macOS (they exit 55 there, pass on Windows and Linux)

## Goal

On macOS, `curl --unix-socket surl.sock http://localhost:8080/hello.txt` fetches the file and exits 0, as on Windows and Linux, and so do `curl-mcp` and `Curl.Api` requests over a Unix socket.

## Context

From Surly cookbook bug report `2026-11-21T0725-Curl-bug-report.md` (CI on `macos-latest`, batches 8-9), finding 1 (Medium). Build tested: Curl `7d167fa10a`; server Surl `9ad60d80c`. The same report went to Surl, since which side fails is not yet known.

**Repro (macOS),** in a folder holding `site/hello.txt`:

```
surl --directory site --unix-socket surl.sock http://localhost:8080/
curl -s --unix-socket surl.sock http://localhost:8080/hello.txt; echo "exit $?"
```

Observed: surl prints `Listening on http://localhost:8080/ over unix://surl.sock`; curl prints nothing and exits 55 (send failure). `curl-mcp` fails `CurlExit`, and a Surl.Api + Curl.Api program aborts (134). Expected: `hello from surl`, exit 0, as on Windows and Linux. The socket path is relative and short, so it is not the 104-byte `sun_path` limit. Cookbook recipes: `http-listen-on-a-unix-socket` and its C# and agent twins (their macOS cases now expect 55).

**First suspect:** an option set on every connected socket that only TCP has: `Socket.NoDelay` (TCP_NODELAY), TCP keep-alive (`--keepalive-time`, curl's default 60 s keep-alive), or `SO_NOSIGPIPE` handling. Linux ignores TCP options on an AF_UNIX socket quietly; macOS returns an error (EOPNOTSUPP or EINVAL), which .NET turns into a `SocketException` the port may map to 55. curl itself skips TCP_NODELAY and TCP keep-alive for Unix sockets (`Curl_sock_assign_addr` / `tcpnodelay` only for `SOCK_STREAM` with an IP family). Check also how the port resolves a relative path (`UnixSocketAddress.cs`), and whether a write before the first read differs on macOS. Reproduce with a loopback Unix-socket server in a unit test (a socket it opens and closes counts as a unit test, per CLAUDE.md, but a byte exchanged makes it an Integration test; choose the project to match) that runs in CI on macOS.

## Acceptance criteria

- [ ] On macOS in CI, an HTTP request over a Unix socket to a loopback server returns the body and exits 0, pinned by a test that runs on macOS, Linux and Windows.
- [ ] No TCP-only socket option is set on a Unix-socket connection on any platform, pinned by a unit test of the options the dialer applies.
- [ ] The Notes name the cause found on macOS (or, if the port was not at fault, show the port's exchange matching curl 8.22.0's on macOS and point at Surl).
- [ ] `dotnet build` is clean and the fast tests pass on all three platforms in CI.

## Notes

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0725, finding 1.