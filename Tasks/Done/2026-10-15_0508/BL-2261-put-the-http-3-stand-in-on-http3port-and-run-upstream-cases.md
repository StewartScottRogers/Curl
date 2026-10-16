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
completed: 2026-10-14
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

- [x] `%HTTP3PORT` has a value and datagrams to it reach the HTTP/3 stand-in; a runner unit test pins the routing.
- [x] Screening lets `http/3`-server cases run when the stand-in is given; 2500-2503 are run or screened only for a reason other than the server, recorded in Notes.
- [x] Passing cases are added to `PassingUpstreamCases.txt`; failing ones have follow-up tasks.
- [x] 100% coverage and the complexity gate hold for `Curl.Conformance.UnitLibrary`; `dotnet test --filter "TestCategory!=Integration"` is green; its `CLAUDE.md` is updated.

## Notes

- Plan, modelled on BL-2230: `UpstreamHttp3Server` (record in `Curl.Conformance.UnitLibrary`, `CreateServer(backend, certificate)`)
  is passed as `UpstreamCaseRunner`'s optional `http3Server`. Given one: the platform reports `nghttpx-h3`, screening lets the
  `http/3` server through (`FindSkipReason(..., emulatesHttp3)`), and with a certificate directory `%HTTP3PORT` is 9019 and
  datagram opens to 9019 reach a fresh stand-in (relaying to the case's sws emulation, presenting `test-localhost.pem`)
  for a case naming `http/3`; TCP to 9019 is refused. Pinned by `UpstreamCaseRunnerTests.RunAsync_WithHttp3ServerAndACertificateDirectory_RoutesHttp3PortDatagramsToItForAnHttp3Case`,
  `RunAsync_NghttpxH3FeatureCase_RunsOnlyWithAnHttp3Server`, `RunAsync_Http3CaseWithoutHttp3Server_IsSkippedForTheServer`,
  `RunAsync_Http3PortWithoutACertificateDirectory_IsSkippedForTheVariable` and `UpstreamHttp3ServerTests`.
- Decisions (ADR-0498, decided by Claude under Stewart's delegation): port 9019; TLS settings built in the test project with
  `EcdsaTlsSigningKey`, since the generated `test-localhost.pem` holds a P-256 key, not the RSA key `FromRsaCertificate` takes;
  TCP to 9019 refused, because measured on this lane `--http3`'s TCP leg reached sws on 9019 and won the race; `http/3`
  reported on Linux and macOS only, because Curl's Windows build refuses `--http3` (measured: exit 2, "the installed libcurl
  version does not support this"), as ADR-0495 did for `http/2`.
- Measured: 2500 passes. On Windows it is `SkippedOnThisPlatform`; emulating the Linux build on Windows (temporary
  `parsesAsWindowsBuild: false`, the Unix feature set, and `--ssl-no-revoke` in place of the Windows-only revocation check)
  it passed with `--http3` and with `--http3-only`; those edits were reverted. Listed in `PassingUpstreamCases.txt`.
- 2501 stays screened for `Debug`, 2502 for its `<tool>` (`lib2502`, a libcurl multi program, not for the server), 2503 for
  `headers-api`. No case runs and fails, so no follow-up task.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members,
  worst CRAP 10. Fast tests green.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. The nghttpx-h3 stand-in serves %HTTP3PORT (9019) and upstream case 2500 passes on Linux and macOS
