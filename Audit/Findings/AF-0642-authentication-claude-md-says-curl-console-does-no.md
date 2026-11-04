---
id: AF-0642
title: Authentication CLAUDE.md says Curl.Console does not yet pass SaslAuthenticator a security-context factory; EngineComposition does
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Authentication.UnitLibrary/CLAUDE.md:SaslAuthenticator-security-context-factory:false-statement
reproduction: none
task: BL-4821
tasks: BL-4821
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed: 2026-11-04
closed-how: reliable-reaudit
closed-by: 2026-11-04_1442.md
---
# AF-0642 - Authentication CLAUDE.md says Curl.Console does not yet pass SaslAuthenticator a security-context factory; EngineComposition does

## Summary

Medium finding from the truthfulness auditor at `Curl.Authentication.UnitLibrary/CLAUDE.md:57`: Authentication CLAUDE.md says Curl.Console does not yet pass SaslAuthenticator a security-context factory; EngineComposition does.

## Evidence

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:57`

The project CLAUDE.md says: 'Without a factory both stay not offered; `Curl.Console` does not pass one yet (BL-852).' That implies SASL GSSAPI and NTLM are not offered in the product. Curl.Engine.UnitLibrary/EngineComposition.cs:190-192 builds the IMAP, POP3 and SMTP handlers with CreateSaslAuthenticator(contexts.Value, ...). EngineComposition.Authentication.cs:144-145 passes securityContexts into new SaslAuthenticator(...), and its doc says 'answering GSSAPI and NTLM on the contexts securityContexts makes (ADR-0184, BL-852)' and 'CreateSecurityContextFactory's in production'. An agent would wrongly believe these mechanisms are switched off in curl.exe.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'does not pass one yet'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'CreateSaslAuthenticator\(contexts'
```

- Expected: The CLAUDE.md says the production composition passes a security-context factory (BL-852 done).
- Actual: CLAUDE.md:57 says Curl.Console does not pass one yet; EngineComposition.cs:190-192 passes contexts.Value for imap, pop3 and smtp.

## Re-audits

- 2026-11-04 | 2026-11-04_0643.md | reproduces: no | Ran the reproduction. Select-String finds no 'does not pass one yet' in Curl.Authentication.UnitLibrary/CLAUDE.md, which now says 'the production composition (EngineComposition) passes one for IMAP, POP3 and SMTP (BL-852)'. EngineComposition.cs:190-192 still passes CreateSaslAuthenticator(contexts.Value, ...) for imap, pop3 and smtp.
- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the reproduction. 'does not pass one yet' has no match in Curl.Authentication.UnitLibrary/CLAUDE.md, which now says 'the production composition (EngineComposition) passes one for IMAP, POP3 and SMTP (BL-852)'. EngineComposition.cs:190-192 call CreateSaslAuthenticator(contexts.Value, ...).

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces.
