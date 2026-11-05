---
id: BL-4863
title: Fix AF-0675: Authentication CLAUDE.md says AWS SigV4 is not wired into a transfer yet; EngineComposition wires it into every HTTP transfer
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4863 — Fix AF-0675: Authentication CLAUDE.md says AWS SigV4 is not wired into a transfer yet; EngineComposition wires it into every HTTP transfer

## Goal

The defect the audit office reported as AF-0675 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0675 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0675-authentication-claude-md-says-aws-sigv4-is-not-wir.md`.

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:64`

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:64`

CLAUDE.md lines 61-64: 'AWS Signature Version 4 (BL-628, ADR-0178): AwsSigV4Signer signs an AwsSigV4Request ... AwsSigV4Headers and AwsSigV4UriEncoding canonicalize. Not wired into a transfer yet (BL-629).' Code: Curl.Engine.UnitLibrary/EngineComposition.cs:180 builds the http/https handler as 'new HttpProtocolHandler(recordingConnector, new HttpSignatureHttpAuthenticator(new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(signingClock ?? TimeProvider.System, ...)), ...)'; Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:23 'internal sealed class AwsSigV4HttpAuthenticator(IHttpAuthenticator otherSchemes, AwsSigV4Signer signer) : IHttpAuthenticator'. An agent reading the CLAUDE.md would believe --aws-sigv4 does nothing and may wire it a second time.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'Not wired into a transfer yet'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new AwsSigV4HttpAuthenticator'
```

- Expected: No 'Not wired into a transfer yet' sentence; the CLAUDE.md says EngineComposition signs HTTP transfers through AwsSigV4HttpAuthenticator.
- Actual: CLAUDE.md:64 '...canonicalize. Not wired into a transfer yet (BL-629).'; EngineComposition.cs:180 '... new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(...)) ...'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
