---
id: BL-5565
title: Stop a WebTransport stream exchange failing with "the datagram channel failed" under load
priority: Normal
assignee: Claude
pipeline: feature
lane: no
depends-on: []
touches: [Curl.Quic.UnitLibrary/QuicConnection.cs, Curl.Extended.Protocol.WebTransport.UnitLibrary]
requirement: none
created: 2026-11-19
completed:
---
# BL-5565 — Stop a WebTransport stream exchange failing with "the datagram channel failed" under load

## Goal

A WebTransport stream exchange that sends no datagram never fails because of the QUIC datagram channel; under load, it succeeds as reliably as it does run alone.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 2 (Medium). Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f`.

**Repro:** `curl --cacert server.pem -d "hello over a stream" webtransport://127.0.0.1:<port>/echo` against `surl --cert ... --key ... webtransport://127.0.0.1:<port>/`, with three such loops running side by side.

**Observed:** about 1 run in 30 exits 56 with `WebTransport session failed: QUIC: the datagram channel failed: The I/O operation has been aborted...`, though the exchange sends no datagram. Run alone it passed 40 of 40.

**Expected:** a stream exchange does not fail because of the datagram channel. It looks like the datagram reader is torn down, or raced, while the session is still in use.

**Where to start:** `Curl.Quic.UnitLibrary/QuicConnection.cs:257`, which turns a datagram-channel error into `MultiplexedConnectionFailedException(CurlExitCode.RecvError, "QUIC: the datagram channel failed: ...")`, and the WebTransport session's start and teardown in `Curl.Extended.Protocol.WebTransport.UnitLibrary`. Find what aborts the datagram read (a cancellation or dispose ordering at session close, or a timer under CPU load) and make an aborted datagram read during normal work or teardown not fail the connection. A real datagram-channel failure while datagrams are in use (`?wt=datagram`) still fails the transfer. Write the reproduction as a test that does not depend on load, for example by forcing the abort ordering through an injected seam, and inject `TimeProvider` for any timing.

## Acceptance criteria

- [x] A unit test reproduces the failure deterministically (the datagram read aborted while a stream exchange is in progress or closing) and passes after the fix.
- [ ] The repro, three loops side by side, passes 100 of 100 runs; the Notes record the run.
- [x] A real datagram-channel failure during a `?wt=datagram` exchange still exits 56 with the datagram-channel text.
- [x] Focused unit tests in `Curl.Quic.UnitTests` or the WebTransport library's `.UnitTests` pin each case.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: on Windows a socket receive whose token the loop cancels (wake on a stream change, timer, stop) can end as SocketException OperationAborted instead of OperationCanceledException; QuicConnection.RunAsync treated that as a channel failure. Fix: IsAbortedRead treats it like a cancellation when the loop itself cancelled the read; a socket error without a loop cancel, or with another code, still fails with exit 56 (datagram-channel text).
- Tests: QuicConnectionTests (wake abort keeps the connection usable, stop abort is no failure, a real failure on a cancelled receive still fails RecvError), via QuicTestLiveChannel.AbortsCancelledReceive / FailsCancelledReceiveWith. Quality run for Curl.Quic.UnitLibrary: 0 failing members.
- The 100-of-100 three-loop repro was NOT run: no surl server is available in a lane. Remove lane: no once an interactive session has run `curl --cacert server.pem -d "hello over a stream" webtransport://127.0.0.1:<port>/echo` 100 times in three loops against surl and recorded it here. The WebTransport library needed no change.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 2.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 36c99139 d24eae70 c32ceec5 0c4acc17 3d836d5e.
- 2026-11-19: Doing -> Backlog. Code fix done and committed; only the 100-run three-loop repro against surl remains, interactive only (lane: no)
