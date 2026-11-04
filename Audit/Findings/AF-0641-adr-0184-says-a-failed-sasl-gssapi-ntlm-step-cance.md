---
id: AF-0641
title: ADR-0184 says a failed SASL GSSAPI/NTLM step cancels with exit 67; SecurityContextSaslExchange throws exit 94
auditor: truthfulness
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md:SecurityContextSaslExchange-failed-step-exit:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0641 - ADR-0184 says a failed SASL GSSAPI/NTLM step cancels with exit 67; SecurityContextSaslExchange throws exit 94

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md:46`: ADR-0184 says a failed SASL GSSAPI/NTLM step cancels with exit 67; SecurityContextSaslExchange throws exit 94.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0184-sasl-gssapi-and-ntlm-run-on-one-security-context-per-exchange.md:46`

Scanner candidate exit-not-in-code. ADR-0184 (Status: Accepted) Decision item 5: 'A failed step answers `null`, so the handler cancels with `*` and exit 67 under ADR-0183's contract. curl fails with exit 94 and sends nothing more (measured); matching it needs the contract to carry the exit, filed as BL-856.' Consequences line 53: '`CurlComposition` still builds `SaslAuthenticator` without a factory until BL-852 hands it the router.' The code it names now does otherwise. Curl.Authentication.UnitLibrary/SecurityContextSaslExchange.cs:79 has GetInitialResponseAsync '?? throw new SaslAuthenticationFailedException(CurlExitCode.AuthError, AuthErrorMessage);', which is exit 94; its doc says 'The context cannot make its first token ... curl 8.21.0 fails the transfer with exit 94, sending nothing more (BL-856).' Curl.Engine.UnitLibrary/EngineComposition.cs:190-192 passes contexts.Value to CreateSaslAuthenticator for IMAP, POP3 and SMTP. ADR-0184 has no amendment recording either change (ADR-0139 has one for BL-781), so a reader takes exit 67 as the current behaviour.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0184-*.md -Pattern 'exit 67|without a factory'; Select-String -Path Curl.Authentication.UnitLibrary/SecurityContextSaslExchange.cs -Pattern 'CurlExitCode\.AuthError'
```

- Expected: ADR-0184 states exit 94 for a context that cannot make its token (or carries an amendment), matching the code.
- Actual: ADR-0184:46 states exit 67 and line 53 says no factory is passed; SecurityContextSaslExchange.cs:79 throws CurlExitCode.AuthError (94).

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
