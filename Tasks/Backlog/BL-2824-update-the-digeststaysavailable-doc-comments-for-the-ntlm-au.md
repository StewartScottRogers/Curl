---
id: BL-2824
title: Update the DigestStaysAvailable doc comments for the NTLM authproblem source
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2824 — Update the DigestStaysAvailable doc comments for the NTLM authproblem source

## Goal

`TransferReport.DigestStaysAvailable` and `HttpRequestOptions.DigestStaysAvailable` describe every `authproblem` source `HttpProtocolHandler.SetsAuthProblem` models, with the right upstream case for each.

## Context

Found by the shift-end review (comment-analyzer, confirmed by code-reviewer) of 9f189b0af (BL-2823, ADR-0709). That commit made `DigestStaysAvailableAfter` in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (~line 2748) true also for a 401 whose bare `NTLM` challenge refuses a request sent with an NTLM answer, and re-cited the Digest-nonce source as test2027 and the NTLM source as test2030. The two property docs it points to were not updated:

- `Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs:276-284` still says the flag means only "a 401 offering Digest without `stale=true` after the transfer already held a Digest nonce" and cites "upstream test2030, BL-2820".
- `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs:148-152` cites "upstream test2030, BL-2820" only.

Both are now false or incomplete about the code. That breaks CLAUDE.md's "say what it does, do what it says" rule.

## Acceptance criteria

- [ ] `TransferReport.DigestStaysAvailable`'s doc names both sources: a non-stale Digest challenge while a nonce was held, and a bare `NTLM` challenge to a request sent with an NTLM `Authorization`. It cites ADR-0709 and BL-2823.
- [ ] The upstream case cited for each source in both docs matches the `SetsAuthProblem` doc in `HttpProtocolHandler.cs`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-19: Created.
