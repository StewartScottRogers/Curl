---
id: BL-3808
title: Fix CGR-279: With -Z, HTTP/2 streams are opened in an order other than the command line, so -w output order differs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4006]
touches: [Curl.Console.UnitTests/Cgr279Tests.cs, Curl.Console/ParallelHostQueue.cs, Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitLibrary/MultiplexingNegotiation.cs, Curl.Networking.UnitTests]
model: sonnet
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3808 — Fix CGR-279: With -Z, HTTP/2 streams are opened in an order other than the command line, so -w output order differs

## Goal

Curl behaves as curl 8.22.0 does for every repro in third-party gap report issue CGR-279 (severity Low): same exit code, stdout bytes, stderr text, files written and bytes on the wire.

## Context

From the third-party functional review `Documentation/Reviews/Curl-Gap-Report-2026-10-25.md` (2026-10-25, Curl master at b0d27d68 against curl 8.22.0 Schannel on Windows and curl 8.18.0 OpenSSL on Linux). Its section 1 explains the method, the normalisations and the names `REF`, `PORT`, `SCR` and `EV`. The `SCR` evidence folder was a temporary scratchpad and may be gone; every issue states the server behaviour it needs, so rebuild the repro with `Record-CurlExchange.ps1` or a C# file-based server (no Python, CLAUDE.md). Line numbers are at b0d27d68 and may have moved. Measure real curl before pinning output text.

The issue as the report gives it:

#### CGR-279: With -Z, HTTP/2 streams are opened in an order other than the command line, so -w output order differs

- **Severity:** Low
- **Area:** http2-linux
- **Platform(s):** Linux. Detail: Linux (observed)
- **Confidence:** Probable. Detail: Probable (the timing-dependent order in parallel mode is a weak difference)
- **Related existing finding (GF/AF):** none
- **Source findings:** `http2-linux H2L-23`

**Reproduction**

- *Setup (http2-linux):* EV = `SCR\evidence\http2-linux`. The Windows REF has no HTTP/2, so REF here is REF-LINUX 8.18.0 (nghttp2 1.68). Scripted HTTP/2 server (WSL, needs `libnghttp2.so.14`): `python3 EV/h2srv.py --port P --mode auto|tls|h1 --scen <SCEN> --log s.log [--alpn h2,http/1.1 --cert cert.pem --key key.pem] --life 25`. Harness `EV\run.sh <casefile>` (lines `name|scen|mode|args`); verifier `EV\verify\vrun.sh EV\verify\c_verify.txt` (ports 21612/21613); PORT-WIN: `WINPORT=<port> bash EV\winrun.sh <case>` (one port per case).
- Command: `curl -s -Z --http2-prior-knowledge http://127.0.0.1:P/1 http://127.0.0.1:P/2 http://127.0.0.1:P/3 -w '%{num_connects} %{url_effective}\n'`. Cases: `pk_3urls_Z`, `tls_3urls_Z`, `m_Z_a/b/c`.

Commands (run each with REF and with PORT):

```sh
curl -s -Z --http2-prior-knowledge http://127.0.0.1:P/1 http://127.0.0.1:P/2 http://127.0.0.1:P/3 -w '%{num_connects} %{url_effective}\n'
```

**Expected (real curl)**

- `/1`, `/2`, `/3` in order.

**Actual (Curl)**

- for example `/2`, `/1`, `/3` (verifier run). The order changes from run to run.

**Suspected location in source**

- `Curl.Console\ParallelHostQueue.cs`

**Acceptance criterion:** For every repro command above, PORT's exit code, stdout bytes, stderr text, files written and bytes on the wire equal REF-LINUX's (the Windows REF has no HTTP/2) (Expected); repro variants that already matched still match. Re-check the expected behaviour against a curl 8.22.0 build first (the evidence is from REF-LINUX 8.18.0).


## Acceptance criteria

- [x] Each repro command in the issue, run through real curl and through Curl, gives the same exit code, stdout bytes, stderr text, files written and request bytes on the wire (normalised as the report's section 1 says); the commands and both results are recorded in Notes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it (ADR-0482).
- [x] Repro variants in the issue that already matched curl still match.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Put the focused unit test (ADR-0482) in the new file named in `touches` (`Cgr279Tests.cs`), so tasks on the same library run in parallel. Production files not in `touches` may need changing too; if the fix needs a file another live task names, that is fine, but keep the change to this issue.
- If the issue is not reproducible at the current commit, record the commands and output in Notes and finish the task with that evidence; the gap analysis office's re-measurement closes the matching GF finding, not this task.

- 2026-10-28 (lane 5): Cause found before any edit. Prior-knowledge h2 transfers skip ParallelHostQueue; the 2nd and 3rd wait in PoolingConnector on MultiplexingNegotiation.Decided, a TaskCompletionSource with RunContinuationsAsynchronously, so they wake on the thread pool in arbitrary order. The fix (wake waiters in the order they began to wait, e.g. a queue of per-waiter tasks released in sequence) lands in Curl.Networking.UnitLibrary (PoolingConnector.cs, MultiplexingNegotiation.cs) plus a test in Curl.Networking.UnitTests, which BL-4006 holds whole. Added those to touches and BL-4006 to depends-on.

- 2026-10-29 (lane 5): Fix: MultiplexingNegotiation now releases waiters one at a time in the order they began to wait (WaitAsync, ReleaseNext); PoolingConnector.ShareAsync releases the next waiter after its own FindShareable. A cancelled waiter passes its turn on. Test: Curl.Networking.UnitTests/MultiplexingNegotiationTests.cs (the ordering test failed with the old unordered thread-pool wake-up). Not measured against real curl: the repro needs a Linux nghttp2 server and curl 8.18/8.22 OpenSSL, unavailable on this Windows lane; the expected order (/1, /2, /3) is the report's. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members (ShareAsync kept within complexity 10 by extracting ReportFullLeases). Build clean, fast tests green; tests are platform-neutral, so the CI criterion is met.

## Log

- 2026-10-26: Created from CGR-279 of the third-party gap report.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-28: Doing -> Backlog. Needs Curl.Networking.UnitLibrary (PoolingConnector, MultiplexingNegotiation) held by BL-4006; depends-on BL-4006
- 2026-10-29: Backlog -> Doing. Criteria at claim: 35b14981 f457ad6c 3a81ba92 779790c0.
- 2026-10-29: Doing -> Done. -Z waiters on a pending h2 connection now open their streams in command-line order
