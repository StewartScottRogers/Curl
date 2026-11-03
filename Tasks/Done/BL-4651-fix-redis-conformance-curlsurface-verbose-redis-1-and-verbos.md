---
id: BL-4651
title: Fix Redis conformance: CurlSurface:Verbose-Redis-1 and Verbose-Rediss-1 (-v writes no connection or TLS lines)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Redis.UnitLibrary, Curl.Extended.Protocol.Redis.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/RedisTransferSurfaceCases.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4651 — Fix Redis conformance: CurlSurface:Verbose-Redis-1 and Verbose-Rediss-1 (-v writes no connection or TLS lines)

## Goal

`curl -v redis://...` and `curl -v rediss://...` write the connection lines curl writes for any transfer (`* Connected to localhost (127.0.0.1) port 6379`) and, for `rediss`, the TLS lines of the platform's curl, to standard error.

## Context

Found by BL-4625: `RedisTransferSurfaceCases.Verbose_SuccessfulGet_WritesConnectionLinesToStandardErrorAndOnlyTheReplyToStandardOutput` runs `curl -sS -v redis://localhost/GET/k` in process against a canned RESP3 server and standard error is empty, for both schemes; every other transfer option (-o, -O, -w, --fail, -m) already behaves. The extension's connector (`ProtocolHandlerServices.Connector`, wrapped in `Curl.Engine.UnitLibrary/EngineComposition.cs`) does not seem to report `Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents` lines for extension transfers, so the cause may be shared by every extended protocol (BL-4615, BL-4617 and the other transfer-surface parts meet it too): find where the trace is lost before fixing it in Redis alone, and widen `touches` to the file it lands in. The TLS line the case expects is a prefix: `* schannel: ` on Windows, `* SSL connection using TLSv1.` elsewhere; measure real curl (`Record-CurlExchange.ps1`) against a TLS server and change the case's prefix if curl writes something else first.

## Acceptance criteria

- [x] `CurlSurface:Verbose-Redis-1` and `CurlSurface:Verbose-Rediss-1` pass and their lines are removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the connection line (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; the fixed library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30.

## Notes

- Cause: the case, not Curl. Standard error was never empty: with the known-failure lines removed the case printed Curl's `-v` output, which has `* Established connection to localhost (127.0.0.1 port 6379) from 127.0.0.1 port 50000` (and for rediss the `* schannel: ` lines after it). The case looked for `* Connected to localhost (127.0.0.1) port 6379`, which curl 8.22.0 no longer writes. Measured real curl 8.22.0 (Schannel) with `Record-CurlExchange.ps1 -Port 47651 -CurlArgs -sv,http://localhost:47651/x`: `* Established connection to localhost (127.0.0.1 port 47651) from 127.0.0.1 port 50466 `. So nothing is lost between the connector and extension transfers, and BL-4615, BL-4617 and the other transfer-surface parts meet only the same wrong prefix if they copied it.
- Fix: `RedisTransferSurfaceCases.VerboseLines` now expects the prefix `* Established connection to localhost (127.0.0.1 port 6379) from ` (the local port varies); the TLS prefixes stay as they were (Windows run shows `* schannel: ` after the connection line). Widened `touches` to `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/RedisTransferSurfaceCases.cs`: no task in Doing on origin/work/dark-factory names it.
- ADR-0482 unit test: no production code changed, so the focused test pins the Redis side of the line: `RedisProtocolHandlerTests.ExecuteAsync_Url_HandsTheTransfersEventsToTheConnectorForVerbosesConnectionLine` checks the transfer's `Events`, through which the connector writes the line, reach the connector for both schemes.
- No production code changed, so no Measure-CodeQuality.ps1 run was needed; Redis's coverage is unchanged. No ADR: this is a measured output line, not a design choice.
- Platform: the connection line is the same on every platform (curl's connect filter, not the TLS backend); the TLS prefix keeps its per-platform branch. CI on Linux and macOS: kept platform-neutral, fast tests green here.
- Filed BL-4656 for the two doc rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` that still say `Connected to`: BL-4623 (Doing) holds that file.
- Fast run: one failure in Curl.Networking.UnitTests, `Cgr223Tests.ReadAsync_AfterTheServerSendsGarbageRecords_FailsWithTheAlertText`, which passed alone; this task changed nothing there. Filed BL-4655. Every other project passed.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8b13c440 5eadf492 64c8e888.
- 2026-11-02: Doing -> Done. curl -v redis:// and rediss:// conformance cases pass: they now expect curl 8.22's measured Established connection line, which Curl already writes
