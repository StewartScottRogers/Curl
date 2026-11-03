---
id: BL-4623
title: Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for xmpp and xmpps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463, BL-4625]
touches: [Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/XmppTransferSurfaceCases.cs, Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4623 — Conformance: curl's transfer options (-v, -o, -O, -w, --fail, -m) for xmpp and xmpps

## Goal

Curl's generic transfer options behave for `xmpp`, `xmpps` as curl's manual says any transfer does, checked through the real curl command line, with every gap fixed or listed with its fix task.

## Context

Schemes: `xmpp`, `xmpps`.

Use the runner BL-4625 adds. There is no XMPP reference server yet: script the stream negotiation the Xmpp extension (`Curl.Extended.Protocol.Xmpp.UnitLibrary`, its ADRs and `CLAUDE.md`) needs with `ScriptedReferenceServer` in the case file.

Split from BL-4614 (itself split from BL-4496, which holds the whole plan; read its Context). Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md` (harness `ExtendedConformanceHarness.RunAsync`, `ScriptedReferenceServer`/`ScriptedDatagramServer` for canned server bytes, `ReferenceRoute.Tcp`/`Tls`/`Udp`, and a fake `TimeProvider` for `-m` as in `Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs`). Take the schemes from each library's `*Extension.cs`, not from this text, if they differ. Expected values come from curl's manual page for curl 8.22.0 (curl.se/docs/manpage.html, "-w", "--fail", "-m", "EXIT CODES") and from each extension's ADRs and `CLAUDE.md`; where an extension's ADR deliberately differs, the case pins the ADR's answer and names it. Requirement IDs are `CurlSurface:<option>-<n>`, rows in `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` (one table, `| ID | Level | Requirement | Covered by |`). One case file per scheme family in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/`, never a central switch. A defect in an extension library or the engine is listed in `KnownFailures.txt` with a filed High task `Fix <Name> conformance: <requirement>` (pipeline feature; touches that library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test pins the fix (ADR-0482), the line is removed).

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/CurlSurface-Transfer-MessagingStreams.md` lists, for each of `xmpp`, `xmpps`, the checks `-v` (connection lines, and TLS lines for TLS schemes, on stderr, nothing extra on stdout), `-o file` in a temporary directory, `-O`, `-w` (`%{exitcode}`, `%{errormsg}`, `%{url}`, `%{url_effective}`, `%{scheme}`, `%{remote_ip}`, `%{remote_port}`, `%{size_download}`, `%{time_total}` on the injected clock), `--fail` and `--fail-with-body` (exit 22 where the library maps a server error to it) and `-m` (exit 28 at the deadline on the injected clock), each with a `CurlSurface:` ID mapped to a test or to `n/a: <reason>`, and the harness's matrix check passes.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/CurlSurface/Transfer/MessagingStreams/XmppTransferSurfaceCases.cs` run curl in process for every one of those schemes and assert stdout, stderr and exit code for each check, naming the expected value's manual-page or ADR source.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix <Name> conformance: ...` task; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [x] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
## Notes
- Cases reuse `TransferSurfaceCaseRun` (BL-4625) with the existing `XmppReferenceServer` (BL-4495) instead of a new `ScriptedReferenceServer` script: it already negotiates the stream, anonymous SASL, bind and the disco#info answer, so the case file names only its route, URL and answer. Default ports 5222 (`xmpp`) and 5223 (`xmpps`, TLS from the first byte, `-k`).
- Expected values: manual page for -v/-o/-O/-w/--fail/-m; ADR-1068 for exit 8 on a stanza error (no server error maps to 22). `-O` names the file `bob@localhost`, the URL's last segment.
- Measured real curl 8.22.0 (Schannel, `Record-CurlExchange.ps1`, HTTP to a loopback server): `-v` writes `* Established connection to localhost (127.0.0.1 port N) from 127.0.0.1 port M`, not `* Connected to ...`. Curl writes the same for xmpp/xmpps, so all 18 cases pass and nothing goes in KnownFailures.txt. The Redis cases (BL-4651, Doing on another lane) still expect the old `* Connected to` line; that expectation, not a lost trace, looks like why they fail.
- Curl.Conformance.Extended.UnitLibrary unchanged, so its coverage criterion holds without a quality run. Cases use only temporary directories and `Path.Combine`, the TLS line prefix per platform as the Redis cases do: platform-neutral. `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Conformance.Extended.UnitTests 1340 passed, 1 skipped).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 02eff282 e38c06bc fe617ef5 be674468 9438adeb ef6c9026.
- 2026-11-02: Doing -> Done. curl's -v, -o, -O, -w, --fail, --fail-with-body and -m are checked for xmpp and xmpps, all 18 cases passing
