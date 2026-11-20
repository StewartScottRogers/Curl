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
completed:
---
# BL-5635 — Find out why the first Curl.Api request in a fresh process takes about 4 seconds, and remove any avoidable part

## Goal

The first `Curl.Api` request in a fresh process takes no longer than its necessary one-time work, and that work is named in the README, so recipe writers know what to expect.

## Context

From Surly cookbook bug report `2026-11-20T1331-Curl-bug-report.md` (all 343 recipes re-verified against Curl `7d167fa10a` and Surl `9ad60d80c`), finding 2 (check).

The first request through `Curl.Api` in a fresh process takes about 4 seconds on an idle Windows 11 machine; later requests take milliseconds. Recipes with short timers had to make an untimed request first. `Curl.Api` is managed code with no native library, so the time is likely one-time set-up: JIT of the engine, composing the protocol and extension registry, loading the Windows certificate store, reading `.curlrc`-like defaults, a DNS or proxy lookup (WinHTTP auto-proxy discovery can take seconds), or a lazy initializer that does more than the request needs.

Measure where the 4 seconds go (an `EventSource` trace or `Stopwatch` timings around composition, the first connect, TLS set-up and the first DNS lookup) for an `http://127.0.0.1` request with no TLS, and for an `https://` one. Remove what a plain-HTTP first request does not need (for example, loading the certificate store or probing the system proxy before any TLS or proxy is used), and document what remains. This is about start-up behaviour a user's timers see, not about matching C curl's speed (Stewart's 2026-10-13 rule).

## Acceptance criteria

- [ ] The Notes break the first-request time down by stage for plain HTTP and for HTTPS on Windows.
- [ ] Any stage a plain-HTTP first request does not need is no longer run for it, and a test pins that (for example, that no certificate store or proxy discovery is touched for a plain loopback HTTP request).
- [ ] The README says what one-time work the first request does and roughly how long it takes.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1331, finding 2 (check).
