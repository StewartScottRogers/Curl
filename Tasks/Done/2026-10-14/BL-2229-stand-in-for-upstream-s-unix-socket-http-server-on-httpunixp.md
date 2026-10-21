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
completed: 2026-10-14
---
# BL-2229 — Stand in for upstream's Unix-socket HTTP server on %HTTPUNIXPATH so cases 1435 and 1436 run

## Goal

Give `%HTTPUNIXPATH` a value and let a Unix-domain-socket dial reach the `sws` emulation in process, so cases 1435 and 1436 (`http-unix`) run.

## Context

Source: Stewart's 2026-10-14 request to raise the gap office's behaviour score (aim 97-99%) by making the shared in-process harness run cases it leaves unmeasured; the 8.22.0 gap run 2026-10-13_1406 had `needs-server:http-unix` 2, and running `Curl.Conformance.UnitTests` on 2026-10-14 names 1435 and 1436 for `%HTTPUNIXPATH` (`--unix-socket %HTTPUNIXPATH http://server-interpreted.example.com/%TESTNUMBER`). The gap tool uses `Curl.Conformance.UnitLibrary`.

Platform: nothing here opens a real Unix socket (the harness dials through `InMemoryServerTcpDialer`, replaced in the tests and the gap tool), so Windows is not limited the way a real `AF_UNIX` listener would be - but the path string must still be platform-neutral: use a drive-less path under the case's `%LOGDIR`-independent form that `runtests.pl` gives (`http.sock` in the log directory); read how Curl's `--unix-socket` hands the path to the connector (search `Curl.Networking.UnitLibrary` / `TcpConnector` / `IConnector` for the Unix-socket target) and make the harness's connector chain answer a Unix target the way it answers a TCP port. If Curl's `--unix-socket` cannot reach an injected dialer at all, say so in Notes and file the Curl-side fix as its own task (not part of this one).

## Acceptance criteria

- [x] `%HTTPUNIXPATH` has a value in `UpstreamCaseRunner`, and a connection whose target is that Unix socket path reaches `SwsHttpServerConnector`; tests in `SwsHttpServerConnectorTests` / `UpstreamCaseRunnerTests` pin it with no real socket.
- [x] Screening lets `http-unix` cases run; cases 1435 and 1436 are no longer screened out. Each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`; each that runs but fails is named in Notes as a real gap, with a follow-up task per distinct cause.
- [x] Platform-neutral tests; `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` shows no member outside a gate; `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` is green; `Curl.Conformance.UnitLibrary/CLAUDE.md` is updated.

## Notes

- `%HTTPUNIXPATH` is `log/server/http.sock` (`UpstreamCaseRunner.HttpUnixPath`). Choice: the relative path runtests.pl's `http.sock` has under its relative log directory, not `%LOGDIR/http.sock`: the harness's absolute `%LOGDIR` (`bin/.../log/test1435-<guid>`) plus `/http.sock` passes 107 bytes, which `UnixSocketAddress.IsTooLong` refuses with exit 6. No real socket is opened, so the path is platform-neutral.
- Curl's `--unix-socket` already reaches an injected dialer (`ITcpDialer.DialUnixSocketAsync`), so no Curl-side task is needed. `InMemoryServerTcpDialer` dialed Unix sockets at port 1, which `NoListenPortConnector` refuses (test1233's port); it now dials the chain at `%HTTPPORT` (8990), so the dial reaches `SwsHttpServerConnector`.
- Screening lets `http-unix` run; 1435 and 1436 pass and are on `PassingUpstreamCases.txt`.
- Tests: `SwsHttpServerConnectorTests.ConnectAsync_ToTheHttpUnixPath_ServesAndRecordsTheRequest`, `InMemoryServerTcpDialerTests.UnixSocket_ToTheHttpUnixPath_ReachesTheSwsStandIn`, `UpstreamCaseRunnerTests.RunAsync_HttpUnixCase_RunsCurlOnTheHttpUnixPath` (1435, 1436); the "no value" screening test now uses `%SOCKSUNIXPATH`.
- `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 0 failing members. Build clean with -warnaserror; fast tests green (Curl.Conformance.UnitTests 2892 passed, 776 skipped).
- For an interactive session (lanes may not read the gap office's folder): the gap office's measuring tool has its own dialer with a `DialUnixSocketAsync`; if it also dials port 1 it must send the Unix dial to `%HTTPPORT` instead, or the gap run will still see 1435 and 1436 fail with "refused the connection". A lane cannot file that task (it touches an audit path).

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
- 2026-10-14: Doing -> Done. %HTTPUNIXPATH reaches the in-memory sws stand-in; upstream cases 1435 and 1436 pass
