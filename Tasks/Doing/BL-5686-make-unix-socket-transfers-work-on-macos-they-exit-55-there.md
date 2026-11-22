---
id: BL-5686
title: Make --unix-socket transfers work on macOS (they exit 55 there, pass on Windows and Linux)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/UnixSocketAddress.cs, Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitTests/TcpDialerUnixSocketAcceptTests.cs]
lane: no
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
- 2026-11-21 (lane 1): Read the dial path: `TcpDialer.DialUnixSocketAsync` sets no socket option at all (no NoDelay, no keep-alive, no QoS; `ApplySocketOptions` is only called from the TCP `DialAsync`), and the wrappers (`LocalBindingTcpDialer`, `SocketOpenerTcpDialer`, `SocketActionGatedTcpDialer`) and `TcpConnector.ConnectOverUnixSocketTracedAsync` pass straight through. So the first suspect (a TCP-only option) is ruled out by reading; the port sets none on any platform. A path is passed unchanged to `UnixDomainSocketEndPoint`, relative or not. Added Integration test `DialUnixSocketAsync_HttpRequestThenReply_CarriesTheRequestOutAndTheReplyBack` (Curl.Networking.IntegrationTests, passes on Windows; runs on macOS in CI) which pins the request/reply exchange over the dialer; committed.
- Not found: the macOS cause. No macOS host is reachable from a lane, so the exit-55 cause (port or Surl) cannot be measured here. Remaining suspects for a macOS run: the engine's HTTP write path after connect (`ConnectionEndPointRecorder.RemoteIpOf` uses `UnixDomainSocketEndPoint.Serialize()[2]`), and Surl's listener. Next step needs the CI log of the macOS cookbook case with `-v`/`--trace` output.
- 2026-11-21 (interactive session, from the Surly session's macOS CI run on macos-latest, Darwin 25.6.0 arm64): the fault is Surl's, not the port's. Surl crashes on its first Unix-socket accept with `System.ArgumentException: Destination is too short. (Parameter 'destination')` in `SocketAsyncEventArgs.FinishOperationAccept`. The port's first request therefore got an empty reply (exit 52); after the crash every connect was refused (exit 7): the port with a relative and an absolute path, and macOS's own `/usr/bin/curl` 8.7.1 with both, so upstream curl fails the same way against the crashed server. The earlier "exit 55" was this sequence seen through the cookbook's harness. Log: the Surly session's `scratchpad/diagnose-unix.log` (152 lines, traces from both curls). Surl tracks the cause as its BL-1565. Nothing to change in the port; the dialer Integration test added above pins the exchange on macOS in CI. Moved to Deferred, not Done: criterion 1 (a body over a Unix socket on macOS) needs a server that does not crash, which is Surl's fix; look again only if the cookbook's macOS case still fails after Surl BL-1565.
- 2026-11-21 (interactive session, correcting the line above): the "nothing to change in the port" conclusion was premature. Surl's new conformance test on both Macs (Surl's Release run 44916418073) shows that upstream curl over `--unix-socket` does NOT crash Surl; only the port's client did. So the port's Unix-socket client presents a peer address that a .NET server's accept (`FinishOperationAccept`) cannot copy, while upstream curl's does not. `TcpDialer.DialUnixSocketAsync` itself only does `new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)` and `ConnectAsync(UnixDomainSocketEndPoint)`, with no bind and no options, so the difference is likely in .NET's own Unix-socket connect on macOS (for example an implicit bind, or the socket address length .NET passes to `connect`, which the server then gets back from `accept`). The line above also wrongly said the dialer Integration test runs on macOS in CI: CI's `Build and test` job runs only the fast tests (`TestCategory!=Integration`); only Curl.Mcp's Integration tests run on every OS.
- Next step (for whoever takes it): add a unit test that runs on macOS in CI: a `Socket` listener on a temporary Unix-socket path whose `AcceptAsync` takes the connection from `DialUnixSocketAsync`, then both close without sending a byte (a loopback socket opened and closed without a byte is a unit test, per CLAUDE.md, so it runs in the fast tests on all three platforms). On macOS it should reproduce `Destination is too short`. Then compare the client socket's `getsockname` and the `connect` address length with what curl passes (`connect(fd, sun, sizeof(struct sockaddr_un))` with an unbound socket) and make the port's client present the same; if .NET gives no control, connect through a raw `connect` call with curl's length. Surl fixes its side too (Surl BL-1565, High).
- 2026-11-21 (lane 1, third claim): Took the next step above as far as a lane can. Added the fast unit test `TcpDialerUnixSocketAcceptTests.DialUnixSocketAsync_ToADotNetListener_IsAccepted` (Curl.Networking.UnitTests, added to `touches`; no other Doing task names it): a .NET `Socket` listener on a temporary path accepts the connection `DialUnixSocketAsync` opens, and both close without a byte, so it runs in CI's fast tests on Windows, Linux and macOS. It passes on Windows. The temporary path is absolute (long on macOS, `/var/folders/...`), so if this passes on macOS while the cookbook's short relative `surl.sock` crashes, the path length of the listener is the trigger. Measured on Windows: .NET reports the accepted socket's peer (the port's unbound client) as a 108-character all-NUL path (`@\0\0...`), i.e. .NET's accept sizes the peer address at the full `sockaddr_un`, not at the listener's address; a peer-address assertion was dropped from the test because that answer depends on the platform. Not done: the macOS cause itself and criteria 1 to 3. The client sets no option and does no bind (read in `TcpDialer.DialUnixSocketAsync`), so nothing in the port's code differs from curl's unbound `connect` that a lane can see or change without a macOS measurement.
- Interactive only. Remove `lane: no` once a macOS run has compared what the server's accept receives from upstream curl and from the port with the same socket path (relative `surl.sock` for both, e.g. `ktrace`/`dtruss` of `connect` and the accept's address length, or this unit test's result on macOS CI with a short relative path), so a lane knows what the port's client must change.

## Log

- 2026-11-21: Created from Surly bug report 2026-11-21T0725, finding 1.
- 2026-11-21: Backlog -> Doing. Criteria at claim: edecd269 dbd35abe b7fc0636 b3b3bfe9.
- 2026-11-21: Doing -> Blocked. Stewart: the port sets no socket option on a Unix dial, so what exits 55 on macOS needs a macOS run - can you send the macOS CI log of curl -v --unix-socket against surl?
- 2026-11-21: Blocked -> Deferred. Not the port: on macOS Surl crashes on its first Unix-socket accept (Destination is too short), and /usr/bin/curl 8.7.1 fails the same way; Surl tracks it as its BL-1565
- 2026-11-21: Deferred -> Backlog. Reopened: upstream curl does not crash Surl on macOS, only the ports .NET client does, so the ports Unix-socket client differs from curls; next step in Notes
- 2026-11-21: Backlog -> Doing. Criteria at claim: edecd269 dbd35abe b7fc0636 b3b3bfe9.
- 2026-11-21: Doing -> Backlog. Interactive only: the macOS cause needs a macOS run comparing the server's accept of upstream curl and of the port with the same socket path; lane test added
- 2026-11-22: Backlog -> Doing. Criteria at claim: edecd269 dbd35abe b7fc0636 b3b3bfe9.
