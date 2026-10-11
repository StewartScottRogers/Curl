---
id: BL-2026
title: Run GF-0060's upstream cases through the gap harness and record why Curl's SASL cancel and downgrade measure as 'got the end'
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Imap.UnitLibrary]
lane: no
requirement: none
created: 2026-10-10
completed: 2026-10-11
---
# BL-2026 — Run GF-0060's upstream cases through the gap harness and record why Curl's SASL cancel and downgrade measure as 'got the end'

## Goal

The cause of GF-0060's failures (upstream test833, 834, 879, 880, 935, 936) is known from the gap harness itself, and is written under this task's Notes and in BL-1990's Notes, so a lane can fix it.

## Context

Interactive only: the gap harness (`Gap/Tools/Measure-UpstreamCases.cs`) and the upstream `tests/data` cases under `%LOCALAPPDATA%\Curl\gap\upstream\8.21.0` are audit-guarded, and lanes may not read them (guard-audit-paths.ps1, ADR-0433).

GF-0060 says Curl sends nothing after `AUTHENTICATE CRAM-MD5` (test833: "expected `*`, got the end") and nothing after `AUTHENTICATE NTLM` (test834: expected the Type 1, got the end), and the same for POP3 and SMTP. BL-1990 replayed all four shapes against this tree's Curl (`Curl.Console\bin\Debug\net10.0\curl.exe`, built from 192ec6f80) on loopback with `Record-CurlExchange.ps1 -Script`, with the server sending exactly what the cases' `<servercmd>` lists (`+ Rubbish`, `334 Rubbish`, the `NO`/`-ERR`/`501` cancel reply, then PLAIN): Curl sent the Type 1 after `+`, `*` after the bad challenge, read the reply, and went on with `AUTHENTICATE PLAIN` / `AUTH PLAIN` and the PLAIN message, as upstream expects. The same is pinned by the `*SaslCancelTests` classes in the IMAP, POP3 and SMTP unit tests.

So the harness's server must answer differently from curl's `ftpserver.pl`. Suspect first: these six cases give the command in quotes with an argument (`REPLY "AUTHENTICATE CRAM-MD5" + Rubbish`, `REPLY "AUTH NTLM" 334 NTLM supported`); if the harness matches only the first word, or does not strip the quotes, it never sends the `+`/`334` and Curl waits until the server ends. Also check the harness builds a current Curl.

Note for test834/880/936: on Windows, Curl answers NTLM through SSPI, as curl's Schannel build does, so its Type 1 is not the `TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=` these `!SSPI` cases expect; the harness should skip `!SSPI` cases when Curl reports SSPI, or measure them on Linux, where Curl's hand-built NTLM sends exactly that Type 1 (`HandBuiltSecurityContextFactoryTests`, `CurlCompositionSmtpNtlmTests`).

Reproduce: `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 833,834,879,880,935,936`, capturing Curl's `-v` stderr for each case.

## Acceptance criteria

- [x] Notes say which Curl binary the harness runs and from which commit it was built.
- [x] Notes say how the harness's server handles a `REPLY "<command with argument>" <reply>` line, with the code line.
- [x] Notes hold Curl's `-v` stderr and exit code for test833 and test834 under the harness.
- [x] Notes say whether the harness skips `!SSPI` cases when Curl runs with SSPI.
- [x] The harness cause is fixed on the `gap` branch, or a lane-eligible Curl task is filed for a Curl cause and added to BL-1990's `depends-on`.

## Notes

- 2026-10-11 (interactive). **Binary:** the harness compiles `Curl.Console` from the gap worktree's source (`#:project ../../Curl.Console/Curl.Console.csproj`) and calls `InProcessCurl.RunAsync`; the run JSON's `commit` names it. This session ran the gap branch at 2fd5b394d. All six cases still fail there with "got the end".
- **`REPLY "<command> <argument>"`:** mishandled. `Curl.Conformance.UnitLibrary/LineProtocolServerCommands.cs:151` (`TryReadReply`) splits the line at its first two blanks, so `REPLY "AUTHENTICATE CRAM-MD5" + Rubbish` is stored under the command `"AUTHENTICATE` with the text `CRAM-MD5" + Rubbish`. `ImapResponder.Answer` looks up `AUTHENTICATE`, finds nothing, and answers `A002 BAD AUTHENTICATE is not dealt with!` (`ImapResponder.cs:126`). `ftpserver.pl` at `curl-8_21_0` keeps such a line as a full-text reply keyed by `AUTHENTICATE CRAM-MD5` (line 2835), and checks it before the command reply (around line 3302).
- **`-v` under the harness** (copies of the cases with `-v --stderr <file>` added). Both exit 67:
  ```
  test833: > A001 CAPABILITY
           < * CAPABILITY IMAP4 AUTH=CRAM-MD5 AUTH=PLAIN pingpong test server
           < A001 OK CAPABILITY completed
           > A002 AUTHENTICATE CRAM-MD5
           < A002 BAD AUTHENTICATE is not dealt with!
           * closing connection #0
           curl: (67) Login denied
  test834: the same with AUTH=NTLM and `> A002 AUTHENTICATE NTLM`, then BAD, exit 67.
  ```
- **`!SSPI`:** the harness does not skip them, and need not. `UpstreamCurlPlatform` leaves `SSPI` out of the features (Curl's `curl -V` does not list it), so 834, 880 and 936 run, and `InProcessCurl` answers NTLM with the hand-built messages (BL-2033).
- **Fix:** the cause is in `Curl.Conformance.UnitLibrary`, which lanes may change, so it is filed as lane-eligible BL-2060 and added to BL-1990's `depends-on`. No change on the `gap` branch.

## Log

- 2026-10-10: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Done. GF-0060 cause recorded: the mail stand-ins store REPLY "AUTHENTICATE X" under a quoted first word, so the server answers BAD; fix filed as lane task BL-2060
