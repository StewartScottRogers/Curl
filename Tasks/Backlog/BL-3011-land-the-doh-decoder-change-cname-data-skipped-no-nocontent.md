---
id: BL-3011
title: Land the DoH decoder change: CNAME data skipped, no NoContent, curl 8.22.0's A and AAAA error lines, with their Networking and Engine tests
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/DnsAnswer.cs, Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs, Curl.Networking.UnitLibrary/DnsMessageFailure.cs, Curl.Networking.UnitLibrary/DnsServerQuery.cs, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohDnsResolver.SubTransferTrace.cs, Curl.Networking.UnitLibrary/DohQueryResult.cs, Curl.Networking.UnitLibrary/DohTraceLines.cs, Curl.Networking.UnitLibrary/DohTraceLines.AnswerDecoding.cs, Curl.Networking.UnitLibrary/DohTraceLines.ExchangeFailures.cs, Curl.Networking.UnitTests/DnsAnswerDecoderTests.cs, Curl.Networking.UnitTests/DohDnsResolverSubTransferTests.cs, Curl.Networking.UnitTests/NetworkingAdversarialTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.AnswerDecoding.cs, Curl.Networking.UnitTests/DohDnsResolverTraceTests.ExchangeFailures.cs, Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3011 — Land the DoH decoder change: CNAME data skipped, no NoContent, curl 8.22.0's A and AAAA error lines, with their Networking and Engine tests

## Goal

The DoH decoder change split from BL-2970 is committed: `DnsAnswerDecoder.Decode` skips CNAME and DNAME data and never reports `NoContent`, and `DohDnsResolver` writes curl 8.22.0's `[DoH] [A|AAAA] error decoding response: ...` lines, with the Networking and Engine tests that pin them.

## Context

BL-2970 was split because its runs spent their cost cap (ADR-0738). Its Notes (in `Tasks/Deferred/BL-2970-*.md`) hold the measurements and the decisions, which are the specification here. Nearly all of the code is written and sits in a stash that the dark factory applies for this task: stash `639766a5e997447486c48d2566c3b59b7f12bd28`, 13 files (DnsAnswer, DnsAnswerDecoder, DnsMessageFailure, DnsServerQuery, DohDnsResolver, DohDnsResolver.SubTransferTrace, DohQueryResult, DohTraceLines, DohTraceLines.AnswerDecoding, DohTraceLines.ExchangeFailures deleted, and the Networking tests DnsAnswerDecoderTests, DohDnsResolverSubTransferTests, NetworkingAdversarialTests). If the shift reports it did not apply, `git diff <sha>^1 <sha>` shows the tracked changes and `git show --stat <sha>^3` the added files.

What is left, all in this task:

1. Port the earlier stash's `DohDnsResolverTraceTests.cs` diff (`git diff 0725a3f99653d7677f0fd10fefdb5c44c4ffb928^1 0725a3f99653d7677f0fd10fefdb5c44c4ffb928 -- Curl.Networking.UnitTests/DohDnsResolverTraceTests.cs`) onto the three split files `DohDnsResolverTraceTests.cs`, `.AnswerDecoding.cs` and `.ExchangeFailures.cs`: the `FormatAddress` tests and the decoded-entry and `CNAME:` line tests go; a decode failure is `[DoH] [DoH] [A] error decoding response: <doh_strerror>`.
2. Change the 8.21.0 DoH lines in `Curl.Engine.UnitTests/EngineCompositionDohSubTransferTraceTests.cs` (lines 59-60 and 77-80) and `EngineCompositionDohTests.cs` (lines 140-143): a decode failure is `* [DoH] [DoH] [AAAA] error decoding response: Unexpected TYPE` after its sub-transfer's lines, with no `a DoH request is completed` line for that query and no hostname/TTL/A lines.
3. Build, run the fast tests of `Curl.Networking.UnitTests` and `Curl.Engine.UnitTests`, and commit. The coverage and quality measurement is BL-3013 and the ADR amendment is BL-3012: do neither here.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pins that `DnsAnswerDecoder.Decode` gives `None` for unit1650's self-pointing CNAME row (46 bytes, `\x07\x03any\xc0\x27\x00`) and for an answer with no record.
- [ ] `DohDnsResolverTraceTests` (its three partial files) pins `[DoH] [DoH] [A] error decoding response: No such name` for an NXDOMAIN answer and the other decode-failure lines, and no test still expects `CNAME:` entries or `FormatAddress`.
- [ ] The two Engine test files named above expect the 8.22.0 lines and pass.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Networking.UnitTests` and `Curl.Engine.UnitTests`.

## Notes

- 2026-10-21: Split from BL-2970. The stash was re-stored under this task's ID (same commit 639766a5e997447486c48d2566c3b59b7f12bd28) so the shift applies it on the first claim.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: cb9752a7 96fe7f29 785e37e5 8b4aeea1.
- 2026-10-21: Doing -> Backlog. Lane 1 could not integrate: push kept being refused. The work is on branch factory/BL-3011-lane-1-20261021-203534; start with git cherry-pick --no-commit factory/BL-3011-lane-1-20261021-203534 and fix it.
