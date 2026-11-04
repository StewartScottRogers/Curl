---
id: BL-4820
title: Fix AF-0641: ADR-0184 says a failed SASL GSSAPI/NTLM step cancels with exit 67; SecurityContextSaslExchange throws exit 94
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-04
completed:
---
# BL-4820 — Fix AF-0641: ADR-0184 says a failed SASL GSSAPI/NTLM step cancels with exit 67; SecurityContextSaslExchange throws exit 94

## Goal

The defect the audit office reported as AF-0641 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0641 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0641-adr-0184-says-a-failed-sasl-gssapi-ntlm-step-cance.md`.

Location: `Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md:46`

Location: `Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md:46`

Scanner candidate exit-not-in-code. ADR-0184 (Status: Accepted) Decision item 5: 'A failed step answers `null`, so the handler cancels with `*` and exit 67 under ADR-0183's contract. curl fails with exit 94 and sends nothing more (measured); matching it needs the contract to carry the exit, filed as BL-856.' Consequences line 53: '`CurlComposition` still builds `SaslAuthenticator` without a factory until BL-852 hands it the router.' The code it names now does otherwise. Curl.Authentication.UnitLibrary/SecurityContextSaslExchange.cs:79 has GetInitialResponseAsync '?? throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage);', which is exit 94; its doc says 'The context cannot make its first token ... curl 8.21.0 fails the transfer with exit 94, sending nothing more (BL-856).' Curl.Engine.UnitLibrary/EngineComposition.cs:190-192 passes contexts.Value to CreateSaslAuthenticator for IMAP, POP3 and SMTP. ADR-0184 has no amendment recording either change (ADR-0139 has one for BL-781), so a reader takes exit 67 as the current behaviour.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0184-*.md -Pattern 'exit 67|without a factory'; Select-String -Path Curl.Authentication.UnitLibrary/SecurityContextSaslExchange.cs -Pattern 'CurlExitCode\.AuthError'
```

- Expected: ADR-0184 states exit 94 for a context that cannot make its token (or carries an amendment), matching the code.
- Actual: ADR-0184:46 states exit 67 and line 53 says no factory is passed; SecurityContextSaslExchange.cs:79 throws CurlExitCode.AuthError (94).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
