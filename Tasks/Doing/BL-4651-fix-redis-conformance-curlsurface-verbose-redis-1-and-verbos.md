---
id: BL-4651
title: Fix Redis conformance: CurlSurface:Verbose-Redis-1 and Verbose-Rediss-1 (-v writes no connection or TLS lines)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary, Curl.Extended.Protocol.Redis.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4651 — Fix Redis conformance: CurlSurface:Verbose-Redis-1 and Verbose-Rediss-1 (-v writes no connection or TLS lines)

## Goal

`curl -v redis://...` and `curl -v rediss://...` write the connection lines curl writes for any transfer (`* Connected to localhost (127.0.0.1) port 6379`) and, for `rediss`, the TLS lines of the platform's curl, to standard error.

## Context

Found by BL-4625: `RedisTransferSurfaceCases.Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput` runs `curl -sS -v redis://localhost/GET/k` in process against a canned RESP3 server and standard error is empty, for both schemes; every other transfer option (-o, -O, -w, --fail, -m) already behaves. The extension's connector (`ProtocolHandlerServices.Connector`, wrapped in `Curl.Engine.UnitLibrary/EngineComposition.cs`) does not seem to report `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents` lines for extension transfers, so the cause may be shared by every extended protocol (BL-4615, BL-4617 and the other transfer-surface parts meet it too): find where the trace is lost before fixing it in Redis alone, and widen `touches` to the file it lands in. The TLS line the case expects is a prefix: `* schannel: ` on Windows, `* SSL connection using TLSv1.` elsewhere; measure real curl (`Record-CurlExchange.ps1`) against a TLS server and change the case's prefix if curl writes something else first.

## Acceptance criteria

- [ ] `CurlSurface:Verbose-Redis-1` and `CurlSurface:Verbose-Rediss-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the connection line (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; the fixed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8b13c440 5eadf492 64c8e888.
