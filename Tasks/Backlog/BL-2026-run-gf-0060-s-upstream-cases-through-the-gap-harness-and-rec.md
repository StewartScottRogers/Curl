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
completed:
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

- [ ] Notes say which Curl binary the harness runs and from which commit it was built.
- [ ] Notes say how the harness's server handles a `REPLY "<command with argument>" <reply>` line, with the code line.
- [ ] Notes hold Curl's `-v` stderr and exit code for test833 and test834 under the harness.
- [ ] Notes say whether the harness skips `!SSPI` cases when Curl runs with SSPI.
- [ ] The harness cause is fixed on the `gap` branch, or a lane-eligible Curl task is filed for a Curl cause and added to BL-1990's `depends-on`.

## Notes

## Log

- 2026-10-10: Created.
