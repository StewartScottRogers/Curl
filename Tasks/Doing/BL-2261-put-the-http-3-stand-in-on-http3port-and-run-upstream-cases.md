---
id: BL-2261
title: Put the HTTP/3 stand-in on %HTTP3PORT and run upstream cases 2500-2503
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2231, BL-2260]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
model: opus
requirement: none
created: 2026-10-14
completed:
---
# BL-2261 — Put the HTTP/3 stand-in on %HTTP3PORT and run upstream cases 2500-2503

## Goal

`%HTTP3PORT` has a value, datagrams to it reach `UpstreamHttp3ServerFactory`'s stand-in (relaying to the case's sws emulation), screening lets `http/3`-server cases run, and upstream cases 2500-2503 are no longer screened out for the server.

## Context

BL-2231 built the stand-in (`Curl.Conformance.Http3Server.UnitLibrary`: `UpstreamHttp3ServerFactory.Create(backend)` returns an `IDatagramConnector` whose every open is a fresh nghttpx-h3 emulation relaying to `backend`). BL-2260 makes in-process Curl send QUIC through the injected datagram connector. Left here, modelled on BL-2230's `UpstreamHttp2Server` (commit 63c9edc9c, ADR-0495):

- An `UpstreamHttp3Server` record in `Curl.Conformance.UnitLibrary` (that library cannot reference the stand-in) with `Http3Port`, routing datagram opens to that port to the stand-in and every other port to the case's tftpd emulation; passed to `UpstreamCaseRunner` and built in `UpstreamConformanceTests` (test project referencing `Curl.Conformance.Http3Server.UnitLibrary`).
- `%HTTP3PORT` in the runner's variables; screening lets the `http/3` server through when one is given, and reports feature `nghttpx-h3` then.
- The stand-in needs a TLS certificate: `Http3ServerTlsSettings.FromRsaCertificate` with upstream's `test-localhost.pem` (as `HttpsServerConnector.LoadCertificate` loads it) and `Http3ServerConnection.DefaultTransportParameters`; build the settings in the test project.
- Upstream cases (curl-8_22_0): 2500 GET with `--resolve` (needs `nghttpx-h3`); 2501 POST needs `Debug` (stays screened); 2503 needs `headers-api` (stays screened); 2502 is a multi-transfer case - check why it is screened. Each that passes goes in `PassingUpstreamCases.txt`; each that runs and fails is named in Notes with a follow-up task per cause.

## Acceptance criteria

- [ ] `%HTTP3PORT` has a value and datagrams to it reach the HTTP/3 stand-in; a runner unit test pins the routing.
- [ ] Screening lets `http/3`-server cases run when the stand-in is given; 2500-2503 are run or screened only for a reason other than the server, recorded in Notes.
- [ ] Passing cases are added to `PassingUpstreamCases.txt`; failing ones have follow-up tasks.
- [ ] 100% coverage and the complexity gate hold for `Curl.Conformance.UnitLibrary`; `dotnet test --filter "TestCategory!=Integration"` is green; its `CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
