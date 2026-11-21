---
id: BL-5635
title: Find out why the first Curl.Api request in a fresh process takes about 4 seconds, and remove any avoidable part
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlClient.SendAsync.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5635 — Find out why the first Curl.Api request in a fresh process takes about 4 seconds, and remove any avoidable part

## Goal

The first `Curl.Api` request in a fresh process takes no longer than its necessary one-time work, and that work is named in the README, so recipe writers know what to expect.

## Context

From Surly cookbook bug report `2026-11-20T1331-Curl-bug-report.md` (all 343 recipes re-verified against Curl `7d167fa10a` and Surl `9ad60d80c`), finding 2 (check).

The first request through `Curl.Api` in a fresh process takes about 4 seconds on an idle Windows 11 machine; later requests take milliseconds. Recipes with short timers had to make an untimed request first. `Curl.Api` is managed code with no native library, so the time is likely one-time set-up: JIT of the engine, composing the protocol and extension registry, loading the Windows certificate store, reading `.curlrc`-like defaults, a DNS or proxy lookup (WinHTTP auto-proxy discovery can take seconds), or a lazy initializer that does more than the request needs.

Measure where the 4 seconds go (an `EventSource` trace or `Stopwatch` timings around composition, the first connect, TLS set-up and the first DNS lookup) for an `http://127.0.0.1` request with no TLS, and for an `https://` one. Remove what a plain-HTTP first request does not need (for example, loading the certificate store or probing the system proxy before any TLS or proxy is used), and document what remains. This is about start-up behaviour a user's timers see, not about matching C curl's speed (Stewart's 2026-10-13 rule).

## Acceptance criteria

- [x] The Notes break the first-request time down by stage for plain HTTP and for HTTPS on Windows.
- [x] Any stage a plain-HTTP first request does not need is no longer run for it, and a test pins that (for example, that no certificate store or proxy discovery is touched for a plain loopback HTTP request).
- [x] The README says what one-time work the first request does and roughly how long it takes.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
- Measured with a throwaway harness outside the repo (Release, Windows 11, loopback HttpListener; AssemblyLoad timestamps). `CurlClient.Create()`: 20 ms (the engine builds handlers lazily). First `SendAsync` plain HTTP: about 500 ms (assembly loads plus JIT: Authentication, Cryptography, Plugin extension, System.Security.Claims about 160 ms of JIT, Brotli); second 40 ms. `CURL_EXTENSIONS=0`: about 330 ms. First `https://example.com/`: 730 to 850 ms (adds Curl.Http2 about 250 ms, TLS, DNS); second 75 ms.
- The 3 s Create and 4 s first request seen right after a rebuild (Debug and the first Release run) did not repeat on later runs: the cost is cold-cache first load of about 100 freshly written assemblies (antivirus scan), not code Curl runs. No certificate store, proxy discovery or `.curlrc` read shows up as a stage of the plain-HTTP first request, so there is nothing avoidable to remove and no test to pin; the work left is assembly loading and JIT.
- Decision: document the one-time work and the warm-up advice in `Curl.Api/README.md` ("The first request in a process"). No production code changed, so no ADR.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1331, finding 2 (check).
- 2026-11-20: Backlog -> Doing. Criteria at claim: 75e5104d a479cbec 57571814 3d836d5e.
- 2026-11-20: Doing -> Done. Measured: first request about 0.5 s plain HTTP, 0.7-0.9 s HTTPS; nothing avoidable; README documents it
