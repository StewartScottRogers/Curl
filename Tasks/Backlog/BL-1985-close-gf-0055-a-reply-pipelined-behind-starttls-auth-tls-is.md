---
id: BL-1985
title: Close GF-0055: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1985 — Close GF-0055: A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0055 (A reply pipelined behind STARTTLS/AUTH TLS is not refused with exit 8, and a pre-authenticated FTP server skips AUTH under --ssl-reqd), so a later gap analysis measures each of `behaviour:test980`, `behaviour:test982`, `behaviour:test983`, `behaviour:test986` as `match`.

## Context

- Finding: GF-0055, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test980`, `behaviour:test982`, `behaviour:test983`, `behaviour:test986`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test980 (SMTP, STARTTLS answered '454' with more replies pipelined): '<verify><protocol> differs at byte 20 (line 3): expected the end, got "AUTH PLAIN AHVzZXIAc2VjcmV0\r\n"'; upstream expects exit 8. test983 (FTP, AUTH answered with pipelined lines): expected the end, got 'AUTH TLS'; upstream expects exit 8. test982 (POP3 STARTTLS pipelined): 'curl did not finish within 20 seconds'. test986 (welcome '230', --ssl-reqd): expected 'AUTH SSL', got 'PWD'; upstream expects exit 64. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 980,982,983,986

Suggestion, copied from the finding:

In Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Pop3.UnitLibrary and Curl.Protocol.Ftp.UnitLibrary, after sending STARTTLS/STLS/AUTH, fail with exit 8 when more bytes are already buffered behind its reply (a pipelined server response), as curl 8.21.0 does, and send nothing more. In FtpSession, when the server greets with 230 (pre-authenticated) and --ssl-reqd is set, still send AUTH SSL / AUTH TLS and fail with exit 64 when both are refused.

## Acceptance criteria

- [ ] `behaviour:test980`: Curl answers what curl 8.21.0 answers, `upstream test980 passes`, so the item measures `match`.
- [ ] `behaviour:test982`: Curl answers what curl 8.21.0 answers, `upstream test982 passes`, so the item measures `match`.
- [ ] `behaviour:test983`: Curl answers what curl 8.21.0 answers, `upstream test983 passes`, so the item measures `match`.
- [ ] `behaviour:test986`: Curl answers what curl 8.21.0 answers, `upstream test986 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
