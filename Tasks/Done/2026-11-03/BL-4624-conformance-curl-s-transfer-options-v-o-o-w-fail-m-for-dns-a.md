---
id: BL-4624
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for dns, and the whole messaging-streams transfer matrix
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4625, BL-4626, BL-4627, BL-4623]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/DnsTransferSurfaceCases.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams-Dns.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-03
---
# BL-4624 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for dns, and the whole messaging-streams transfer matrix

## Goal

Curl's generic transfer options behave for `dns` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `dns`.

Use the runner BL-4625 adds. There is no DNS reference server yet: answer the query `Curl.Extended.Protocol.DnsQuery.UnitLibrary` sends with a datagram server in the case file (copying the query ID). This is the last part of BL-4614: once the dns rows are in, check that the matrix lists every check for all ten schemes (`mqtt5`, `mqtt5s`, `mqtt5+ws`, `redis`, `rediss`, `xmpp`, `xmpps`, `coap`, `coaps`, `dns`).

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` holds every check for all ten schemes `mqtt5`, `mqtt5s`, `mqtt5+ws`, `redis`, `rediss`, `xmpp`, `xmpps`, `coap`, `coaps`, `dns`, each mapped to a test or `n/a: <reason>`.
- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `dns`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/DnsTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

- BL-4664 split the old `CurlSurface-Transfer-MessagingStreams.md` page by scheme family into `CurlSurface-Transfer-MessagingStreams-{Redis,Xmpp,Mqtt5,Coap,Dns}.md` before this claim, so the criteria naming the old page are met by those pages (this task's `touches` names the new `...-Dns.md`); the matrix check covers them all. Checked: every one of the ten schemes has `Verbose`, `Output`, `RemoteName`, two `WriteOut`, two `Fail`, `FailWithBody` and `MaxTime` rows across the pages, each mapped to a test.
- Cases: `DnsTransferSurfaceCases.cs` drives `dns://localhost/example.com` over the UDP route (`TransferSurfaceRoute.Udp(53, ...)`) against a datagram server in the case file that copies the query's message ID and question and answers an A record `192.0.2.1` (NOERROR) or nothing (NXDOMAIN). The expected dig-like text is built from the message ID the server received, since the ID is random. `-m` uses the CoAP cases' pattern (TimerJumpingClock, silent datagram server).
- Decisions (sensible defaults, no ADR needed): `--fail`, `--fail-with-body` and `--fail -w` on NXDOMAIN pin exit 0 with the answer written, as ADR-1140 and `DnsQueryProtocolHandler` say (any response code ends with exit 0, as dig does). `-m 2`, not 5, so curl's deadline falls before the UDP transport's 5-second wait for an answer and the run does not depend on which timer fires first.
- Measured on Curl: 9 of 10 rows pass. `-v` writes nothing at all to standard error (no connection line): listed in KnownFailures.txt with filed High task BL-4672.
- `Curl.Conformance.Extended.UnitLibrary` unchanged, so its quality criterion holds without a run. Tests are platform-neutral (temporary directory through Path.Combine, no OS text or paths); CI on Linux and macOS is covered by that and the green fast run here (115 test assemblies, 0 failed; Extended 1503 passed, 1 skipped). `dotnet build Curl.slnx -warnaserror` clean.

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 9a0bb0a0 f34d0ac2 a482dc4c fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-03: Doing -> Done. curl's transfer options checked for dns; all ten messaging-streams schemes have every check; dns -v listed under BL-4672
