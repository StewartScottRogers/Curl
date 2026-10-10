---
id: BL-1936
title: Bring Curl.Conformance.UnitLibrary's responders and scripts back under the coverage and complexity gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1937, BL-1938, BL-1939, BL-1940, BL-1941]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed:
---
# BL-1936 — Bring Curl.Conformance.UnitLibrary's responders and scripts back under the coverage and complexity gates

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports no failing member: 100% branch coverage and complexity of at most 10 everywhere.

## Context

BL-1929's measurement (2026-10-09) found 28 failing members, none in BL-1929's files: branch gaps and complexity 56-72 in `Pop3Responder.AnswerByDefault` and `SmtpResponder.AnswerByDefault`, branch gaps in `SmtpResponder.Mail`, `IsAddressCharacter`, `Data` and the three `IsBase64Line` lambdas (Imap, Smtp, Pop3), and complexity 12-24 in `UpstreamTest610Script.RunVerbs`, `UpstreamTest613Script.CanonicalLine`/`RemoveFolder`/`Postprocess`, `SocksServerConnection` (Socks5 request, Socks4, AnswerAsync), `LineProtocolReplyData.Select`, the three `TrySplitCommand`s, `LineProtocolServerCommands.Read`, `FtpControlChannelResponder.SwitchDirectory`, `UpstreamCaseRunner.RunScreenedAsync`, `UpstreamCaseScreening.InternetHost`, `SmtpResponder.IsAddress`/`Recipient`/`Verify`, `SwsHttpServerConnection.ReadWithNothingSentAsync` and `ImapResponder.Append`. Split methods (a lookup table of commands, say) and add the missing branch tests; never raise a threshold.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary` reports 0 failing members.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

- 2026-10-09: 28 members across 5 clusters is more than one run's budget; split into BL-1937..BL-1941. This task stays as the final check that Measure-CodeQuality reports 0 failing members.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Backlog. Split into BL-1937, BL-1938, BL-1939, BL-1940, BL-1941; depends on them.
- 2026-10-10: Backlog -> Doing.
