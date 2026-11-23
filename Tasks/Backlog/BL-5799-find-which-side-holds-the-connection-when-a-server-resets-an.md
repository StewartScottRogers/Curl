---
id: BL-5799
title: Find which side holds the connection when a server resets an HTTP download mid-body on macOS, and make Curl exit 56 at once if it is Curl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ConnectionStream.cs, Curl.Networking.IntegrationTests]
requirement: none
lane: no
created: 2026-11-22
completed:
---
# BL-5799 — Find which side holds the connection when a server resets an HTTP download mid-body on macOS, and make Curl exit 56 at once if it is Curl

## Goal

When an HTTP server aborts a connection mid-body (a TCP reset after N bytes), Curl on macOS ends at once with exit 56 `Recv failure: Connection reset by peer`, every time, as it does on Windows and Linux and as upstream curl does on macOS; or, if measurement shows the reset never reaches the client on macOS, the Notes prove it is the server's (Surl's) and say so.

## Context

From Surly cookbook bug report `2026-11-22T2247-Curl-bug-report.md` (batch 13 CI on macOS, run 45115316914), finding 1. Build tested: Curl `2145a49eae`, Surl v0.1.5 `d64faa4bb`, macos-latest.

Repro on macOS: `surl --directory site http://127.0.0.1:8080/ --fault "drop;after-bytes=4000000"` with `site/big.txt` of 5,000,001 bytes; `curl -sS --max-time 30 -o dl/big.txt http://127.0.0.1:8080/big.txt` sometimes exits 56 at once and sometimes never ends until `--max-time` (exit 28); with `--retry 2 --retry-all-errors --retry-delay 1` the run took over 60 s. Intermittent: other cut requests in the same run exited 56 at once. Windows and Linux always exit 56 at once. Curl.Api on macOS was seen to hang the same way earlier (a body read that never ended).

Measure which side holds the connection. This machine is Windows, so iterate through CI on macos-latest (as BL-5686 did: a probe as a C# file-based app or a test on a throwaway branch, deleted after, which needs Stewart's OK to delete):
1. A loopback .NET server that sends N bytes then resets (`LingerState = new(true, 0)` + `Close()`, and also `Shutdown` + `Close` variants, and a server that keeps the socket open, to tell them apart), against Curl's console, `/usr/bin/curl` (upstream) and `nc`, many iterations each; record per client whether it ends with a reset at once, an EOF, or hangs. `lsof`/`netstat -an` on the hanging case shows whether the server's side is still ESTABLISHED.
2. If upstream curl always ends at once and Curl sometimes hangs, it is Curl: likely a read path on macOS that misses the reset (for example a `ReadAsync` on a `NetworkStream`/`SslStream` that does not complete when the kernel reports `ECONNRESET` after the receive buffer drains, a poll/`Socket.Poll` loop ignoring `SelectError`, or a pending read that is not cancelled). Fix it in the networking read path and pin it with an Integration test (a loopback server that sends bytes then resets; it carries `[TestCategory("Integration")]` and lives in `Curl.Networking.IntegrationTests`, which CI runs on all three platforms).
3. If upstream also hangs, or the server's socket is still open, it is Surl's (its `drop` fault on macOS): say so in the Notes with the evidence, and the interactive session tells the Surly session.

## Acceptance criteria

- [ ] The Notes record, per client (Curl, upstream curl, nc) on macOS, how often a mid-body reset ends at once vs hangs, over at least 50 iterations, with the CI run ids.
- [ ] If Curl is at fault: Curl ends with exit 56 at once in every iteration on macOS, Linux and Windows, pinned by an Integration test that runs on all three in CI; the Notes name the cause.
- [ ] If Curl is not at fault: the Notes show the evidence (the server's socket state or upstream curl hanging the same way), and the task is closed without a product change.
- [ ] Any throwaway probe branch is listed in the Notes for deletion; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2247, finding 1 (interactive: needs macOS CI iteration).