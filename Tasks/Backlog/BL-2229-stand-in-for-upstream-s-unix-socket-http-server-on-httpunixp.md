---
id: BL-2229
title: Stand in for upstream's Unix-socket HTTP server on %HTTPUNIXPATH so cases 1435 and 1436 run
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-14
completed:
---
# BL-2229 — Stand in for upstream's Unix-socket HTTP server on %HTTPUNIXPATH so cases 1435 and 1436 run

## Goal

Give `%HTTPUNIXPATH` a value and let a Unix-domain-socket dial reach the `sws` emulation in process, so cases 1435 and 1436 (`http-unix`) run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had `needs-server:http-unix` 2, and running `Curl.Conformance.UnitTests` on 2026-10-14 names 1435 and 1436 for `%HTTPUNIXPATH` (`--unix-socket %HTTPUNIXPATH http://server-interpreted.example.com/%TESTNUMBER`). The gap tool uses `Curl.Conformance.UnitLibrary`.

Platform: nothing here opens a real Unix socket (the harness dials through `InMemoryServerTcpDialer`, replaced in the tests and the gap tool), so Windows is not limited the way a real `AF_UNIX` listener would be - but the path string must still be platform-neutral: use a drive-less path under the case's `%LOGDIR`-independent form that `runtests.pl` gives (`http.sock` in the log directory); read how Curl's `--unix-socket` hands the path to the connector (search `Curl.Networking.UnitLibrary` / `TcpConnector` / `IConnector` for the Unix-socket target) and make the harness's connector chain answer a Unix target the way it answers a TCP port. If Curl's `--unix-socket` cannot reach an injected dialer at all, say so in Notes and file the Curl-side fix as its own task (not part of this one).

## Acceptance criteria

- [ ] `%HTTPUNIXPATH` has a value in `UpstreamCaseRunner`, and a connection whose target is that Unix socket path reaches `SwsHttpServerConnector`; tests in `SwsHttpServerConnectorTests` / `UpstreamCaseRunnerTests` pin it with no real socket.
- [ ] Screening lets `http-unix` cases run; cases 1435 and 1436 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [ ] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

## Log

- 2026-10-14: Created.
