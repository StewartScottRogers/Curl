---
id: AF-0675
title: Authentication CLAUDE.md says AWS SigV4 is not wired into a transfer yet; EngineComposition wires it into every HTTP transfer
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Authentication.UnitLibrary/CLAUDE.md:AwsSigV4Signer:false-statement
reproduction: none
task: BL-4863
tasks: BL-4863
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: reliable-reaudit
closed-by: 2026-11-05_0819.md
---
# AF-0675 - Authentication CLAUDE.md says AWS SigV4 is not wired into a transfer yet; EngineComposition wires it into every HTTP transfer

## Summary

Medium finding from the truthfulness auditor at `Curl.Authentication.UnitLibrary/CLAUDE.md:64`: Authentication CLAUDE.md says AWS SigV4 is not wired into a transfer yet; EngineComposition wires it into every HTTP transfer.

## Evidence

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:64`

CLAUDE.md lines 61-64: 'AWS Signature Version 4 (BL-628, ADR-0178): AwsSigV4Signer signs an AwsSigV4Request ... AwsSigV4Headers and AwsSigV4UriEncoding canonicalize. Not wired into a transfer yet (BL-629).' Code: Curl.Engine.UnitLibrary/EngineComposition.cs:180 builds the http/https handler as 'new HttpProtocolHandler(recordingConnector, new HttpSignatureHttpAuthenticator(new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(signingClock ?? TimeProvider.System, ...)), ...)'; Curl.Engine.UnitLibrary/AwsSigV4HttpAuthenticator.cs:23 'internal sealed class AwsSigV4HttpAuthenticator(IHttpAuthenticator otherSchemes, AwsSigV4Signer signer) : IHttpAuthenticator'. An agent reading the CLAUDE.md would believe --aws-sigv4 does nothing and may wire it a second time.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'Not wired into a transfer yet'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'new AwsSigV4HttpAuthenticator'
```

- Expected: No 'Not wired into a transfer yet' sentence; the CLAUDE.md says EngineComposition signs HTTP transfers through AwsSigV4HttpAuthenticator.
- Actual: CLAUDE.md:64 '...canonicalize. Not wired into a transfer yet (BL-629).'; EngineComposition.cs:180 '... new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(...)) ...'

## Re-audits

- 2026-11-05 | 2026-11-05_0819.md | reproduces: no | Select-String for 'Not wired into a transfer yet' in Curl.Authentication.UnitLibrary/CLAUDE.md found no match. CLAUDE.md:64-66 now says EngineComposition puts AwsSigV4HttpAuthenticator in front and signs only when --aws-sigv4 is given. EngineComposition.cs:180 still builds 'new AwsSigV4HttpAuthenticator(httpAuthenticator.Value, new AwsSigV4Signer(...))' for the http/https handler, so the document and the code agree.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces.
