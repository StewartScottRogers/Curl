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
completed: 2026-11-23
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

- [x] The Notes record, per client (Curl, upstream curl, nc) on macOS, how often a mid-body reset ends at once vs hangs, over at least 50 iterations, with the CI run ids.
- [x] If Curl is at fault: Curl ends with exit 56 at once in every iteration on macOS, Linux and Windows, pinned by an Integration test that runs on all three in CI; the Notes name the cause.
- [x] If Curl is not at fault: the Notes show the evidence (the server's socket state or upstream curl hanging the same way), and the task is closed without a product change.
- [x] Any throwaway probe branch is listed in the Notes for deletion; `dotnet build` is clean and the fast tests pass.

## Notes

**Verdict: not Curl's.** On macOS the kernel sometimes never delivers an abortive close (RST) to the client, so every client hangs the same way: Curl, upstream curl and `nc` alike. The second criterion does not apply (Curl is not at fault); there is nothing in Curl's read path to fix, and no product change was made.

Probe: a .NET loopback server (throwaway branch `factory/bl-5799-probe`: `probe-bl5799/probe.cs`, run by `.github/workflows/bl5799-probe.yml` on that branch only) sends `Content-Length: 5000001`, 4,000,000 body bytes, then ends the connection as `reset` (`LingerState(true,0)` + `Close`), `shutdown` (`Shutdown(Send)` + `Close`) or `hold` (socket left open 40 s). Clients: Curl native AOT (`dotnet publish -r <rid>`), Curl JIT (`dotnet curl.dll`), the system `/usr/bin/curl` and `nc`; each curl ran with `-sS --max-time 30 -o <file>`. CI run 45120404417 (2026-11-23), Curl at `23bb970da2`.

macOS (macos-latest, arm64; `/usr/bin/curl` 8.7.1 SecureTransport), reset mode, 50 iterations each:

| Client | Ended at once (<5 s) | Hung | Exit codes |
| --- | --- | --- | --- |
| Curl native AOT | 27 | 23 | 56 x27, 28 x23 |
| Curl JIT | 22 | 28 | 56 x22, 28 x28 |
| upstream curl 8.7.1 | 29 | 21 | 56 x29, 28 x21 |
| nc | 45 | 5 | 0 x45, killed at 40 s x5 |

Linux (ubuntu-latest; `/usr/bin/curl` 8.18.0 OpenSSL), reset mode, 50 iterations each: Curl AOT 50/50 exit 56 at once (max 0.30 s), Curl JIT 50/50 exit 56 (max 0.68 s), upstream 50/50 exit 56 (max 0.01 s), nc 50/50 ended at once. Shutdown mode (3 each): every curl exited 18 at once on both OSes. Hold mode (3 each): every curl exited 28 on both, as expected. Windows (local, `curl.exe` 8.x Schannel, 3 iterations): exit 56 at once.

Socket state on a hang (macOS, snapshot 5 s in, `netstat -an` and `lsof -iTCP:<port>`), the same for upstream curl and Curl:

```
tcp4  0  0  127.0.0.1.49327  127.0.0.1.49326  ESTABLISHED   <- client, Recv-Q 0
tcp4  0  0  127.0.0.1.49326  *.*              LISTEN        <- only the server's listener
curl 26851 ... TCP 127.0.0.1:49327->127.0.0.1:49326 (ESTABLISHED)
```

The server's connection socket is gone (closed with the RST), yet the client's side stays ESTABLISHED with nothing to read: the reset never reached the client's TCP stack, so no read call in any client could see it. Hung runs typically had 3,997,696 of the 4,000,000 bytes sent (the RST discarded the server's unsent tail), which fits an RST whose sequence number lies past the client's `RCV.NXT` being dropped by macOS (RFC 5961-style RST validation) rather than resetting the connection.

For Surl (the interactive session tells the Surly session): an abortive close mid-body on macOS cannot be observed reliably by any client, so its `drop;after-bytes=N` fault cannot be expected to give exit 56 every time there. A graceful `Shutdown(Send)` + `Close` gives exit 18 at once on every OS. The earlier Curl.Api body-read hang on macOS has the same explanation.

No build or test change was made; CI run 45120601365 on `ca3ce7072e` (this task's claim commit) passed the build and fast tests on Windows, Linux and macOS.

Branch to delete (needs Stewart's OK): `factory/bl-5799-probe`.

## Log

- 2026-11-22: Created from Surly bug report 2026-11-22T2247, finding 1 (interactive: needs macOS CI iteration).
- 2026-11-22: Backlog -> Doing. Criteria at claim: 45325176 eeb2629c c7f736e4 0c9767aa.
- 2026-11-23: Doing -> Done. Not Curl's: on macOS the RST of an abortive close sometimes never reaches the client; upstream curl hangs 21/50, Curl 23/50, nc 5/50 (run 45120404417); criterion 2 not applicable; no product change
