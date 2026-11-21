---
id: BL-5669
title: Correct the StartsWithAnEmptyUser and OriginCredentialOf doc comments for the AWS SigV4 case
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5669 — Correct the StartsWithAnEmptyUser and OriginCredentialOf doc comments for the AWS SigV4 case

## Goal

The doc comments of `HttpProtocolHandler.StartsWithAnEmptyUser` and its caller `OriginCredentialOf` describe every case the code handles, the AWS SigV4 one included, each with its own reason.

## Context

- `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs:374-396` (BL-5658, AF-0923). Found by the shift-end review's code-reviewer.
- The AWS SigV4 clause was spliced in before "the Schannel build starts NTLM with the default Windows credentials, the OpenSSL build sends no `Authorization`", so that NTLM-only explanation now reads as if it described the AWS case. A stray `<br/>` sits mid-sentence. `OriginCredentialOf`'s summary still lists only the Digest and NTLM cases. Under CLAUDE.md's "say what it does, do what it says" a wrong doc comment is a defect.

## Acceptance criteria

- [x] The `StartsWithAnEmptyUser` summary lists the Digest, NTLM (Windows only) and AWS SigV4 (every platform) cases, each with its own explanation, and has no `<br/>`.
- [x] The `OriginCredentialOf` summary names the AWS SigV4 case.
- [x] No code changes; `dotnet build` is clean and the fast tests pass.

## Notes

Doc-comment only change in HttpProtocolHandler.cs; build clean, Http unit tests 2731 passed. No production code changed, so no quality run was needed.

## Log

- 2026-11-21: Created by the shift-end review (ADR-0710) of 8c81a2532a...1f330a1f28.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 0a98328b d29c4cf1 31087550.
