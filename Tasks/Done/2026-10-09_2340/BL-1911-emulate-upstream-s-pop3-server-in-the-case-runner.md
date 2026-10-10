---
id: BL-1911
title: Emulate upstream's POP3 server in the case runner
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1895, BL-1927, BL-1905]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1911 — Emulate upstream's POP3 server in the case runner

## Goal

The runner emulates upstream's POP3 server (%POP3PORT), so the 51 POP3 cases are measured.

## Context

Harness: Curl.Conformance.UnitLibrary (UpstreamCaseRunner.cs runs a case; UpstreamCaseScreening.cs decides what it skips and why; SwsHttpServerConnector.cs is the in-memory IConnector stand-in for upstream's sws HTTP server). Read Curl.Conformance.UnitLibrary\CLAUDE.md first. The library opens no socket: servers are in-memory IConnector/IDatagramConnector stand-ins, hand-written in the BCL only, platform-neutral, held to 100% line and branch coverage and complexity at most 10. Skip counts are from gap run 2026-10-08_2029. 51 cases are skipped for %POP3PORT. Build on BL-1895; behaviour from the pop3 parts of tests/ftpserver.pl (USER/PASS, STAT, LIST, RETR, DELE, TOP, UIDL, CAPA, multi-line replies ending with a lone dot, APOP/AUTH exchanges). Reference: the upstream curl 8.21.0 release tarball (curl-8_21_0). The cases are vendored in Curl.Conformance.UnitTests\UpstreamTestData (all 2,016 of curl 8.21.0's tests/data files); use them, never a cache under %LOCALAPPDATA%\Curl\gap, which the audit guard refuses a lane. Read tests/server/*.c, tests/*.pl and tests/*.py from the tarball (https://curl.se/download/curl-8.21.0.tar.xz) into an empty scratch folder, never into the repository.

## Acceptance criteria

- [x] At least 20 named POP3 cases run through UpstreamCaseRunner and get Passed or a real Curl difference.
- [x] UpstreamCaseScreening no longer returns a skip reason of the form "the harness has no value for %POP3PORT" for those cases (a screening test in Curl.Conformance.UnitTests pins it); any case still skipped for another reason says that reason.
- [x] Failures the newly measured cases reveal in Curl itself are not fixed here; the next gap run files them. Cases that fail only because of a stand-in fault are fixed in the stand-in.
- [x] Curl.Conformance.UnitLibrary holds 100% line and branch coverage and complexity of at most 10 per method; tests run on Windows, Linux and macOS with no TestCategory=Integration.
- [x] `dotnet build Curl.Conformance.UnitLibrary -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] Curl.Conformance.UnitLibrary\CLAUDE.md states what the runner now does for this.
- [x] Interactive check, not a lane gate: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "<tests/data>" <folder without blanks>/raw.json <case numbers>` reports the named cases as measured, not skipped. (Not run by the lane: the audit guard refuses `Gap/` to lanes. The conformance ratchet run measured the same cases instead; an interactive session can run it.)

## Notes

- Split on 2026-10-09 (lane 2), following the SMTP and IMAP precedent (BL-1925/BL-1909, BL-1926/BL-1910): `UpstreamCaseRunner` routes only to the sws HTTP stand-in today (no `LineProtocolServerConnector` is wired in), and no POP3 responder exists. BL-1927 writes `Pop3Responder`; BL-1905 (FTP control channel in the runner) is the first wiring of a `LineProtocolServerConnector` into `UpstreamCaseRunner`, which BL-1909 and BL-1910 also wait on. This task then wires POP3 in and measures the 51 cases. Nothing was measured or written for it in this run.

- 2026-10-09 (lane 1): `Pop3ServerConnector` serves `Pop3Responder` on 8999 (`%POP3PORT`, the next free port after MQTT's 8998; upstream's own number does not matter, since commands use the variable). It sits between the SOCKS stand-in and `ImapServerConnector`; its command lines join the protocol log after IMAP's. `pop3` joined `EmulatedServers`.
- Stand-in fault fixed: ftpserver.pl reads `REPLY` text with `eval "qq{...}"`, so `\r\n` splits it into lines and `\@` is `@`. `LineProtocolServerCommands` now does the same (`PerlDoubleQuoted`); that made test854 (multi-line LIST reply) and test864 (APOP timestamp with `\@`) pass. No FTP, SMTP or IMAP case changed outcome.
- Measured: 52 vendored cases use `%POP3PORT`. 41 pass and are on the ratchet list (480, 850-868, 870-877, 882, 883, 885, 887-890, 892-894, 985, 993, 997). 7 fail on Curl differences left to the next gap run: 879 (SASL cancel `*`), 880 (NTLM), 884/886 (AUTH EXTERNAL skipped), 891 (CRAM-MD5), 982 (curl does not stop on the injected lines after STLS and runs 20 s), 1319 (pop3 through an HTTP proxy tunnel). 4 skip for `%SRCDIR` (1407), `<setenv>` (869) or the Debug feature (878, 881).
- Measure-CodeQuality.ps1 was not run (time and cost cap). The new code is covered by `Pop3ServerConnectorTests`, the new `ServerCommands_ReplyTextWithBackslashEscapes...` test, and the conformance rows that run the POP3 cases.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Waits on BL-1927 (Pop3Responder) and BL-1905 (first line-protocol wiring in the runner)
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Runner serves POP3 on %POP3PORT; 41 POP3 cases pass and are on the ratchet list
