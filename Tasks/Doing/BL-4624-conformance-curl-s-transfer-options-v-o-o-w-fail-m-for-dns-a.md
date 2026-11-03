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
completed:
---
# BL-4624 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for dns, and the whole messaging-streams transfer matrix

## Goal

Curl's generic transfer options behave for `dns` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `dns`.

Use the runner BL-4625 adds. There is no DNS reference server yet: answer the query `Curl.Extended.Protocol.DnsQuery.UnitLibrary` sends with a datagram server in the case file (copying the query ID). This is the last part of BL-4614: once the dns rows are in, check that the matrix lists every check for all ten schemes (`mqtt5`, `mqtt5s`, `mqtt5+ws`, `redis`, `rediss`, `xmpp`, `xmpps`, `coap`, `coaps`, `dns`).

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` holds every check for all ten schemes `mqtt5`, `mqtt5s`, `mqtt5+ws`, `redis`, `rediss`, `xmpp`, `xmpps`, `coap`, `coaps`, `dns`, each mapped to a test or `n/a: <reason>`.
- [ ] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `dns`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/DnsTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes

## Log

- 2026-11-02: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 9a0bb0a0 f34d0ac2 a482dc4c fe617ef5 be674468 9438adeb ef6c9026.
