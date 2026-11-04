---
id: BL-4821
title: Fix AF-0642: Authentication CLAUDE.md says Curl.Console does not yet pass SaslAuthenticator a security-context factory; EngineComposition does
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Authentication.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4821 — Fix AF-0642: Authentication CLAUDE.md says Curl.Console does not yet pass SaslAuthenticator a security-context factory; EngineComposition does

## Goal

The defect the audit office reported as AF-0642 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0642 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0642-authentication-claude-md-says-curl-console-does-no.md`.

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:57`

Location: `Curl.Authentication.UnitLibrary/CLAUDE.md:57`

The project CLAUDE.md says: 'Without a factory both stay not offered; `Curl.Console` does not pass one yet (BL-852).' That implies SASL GSSAPI and NTLM are not offered in the product. Curl.Engine.UnitLibrary/EngineComposition.cs:190-192 builds the IMAP, POP3 and SMTP handlers with CreateSaslAuthenticator(contexts.Value, ...). EngineComposition.Authentication.cs:144-145 passes securityContexts into new SaslAuthenticator(...), and its doc says 'answering GSSAPI and NTLM on the contexts securityContexts makes (ADR-0184, BL-852)' and 'CreateSecurityContextFactory's in production'. An agent would wrongly believe these mechanisms are switched off in curl.exe.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Authentication.UnitLibrary/CLAUDE.md -Pattern 'does not pass one yet'; Select-String -Path Curl.Engine.UnitLibrary/EngineComposition.cs -Pattern 'CreateSaslAuthenticator\(contexts'
```

- Expected: The CLAUDE.md says the production composition passes a security-context factory (BL-852 done).
- Actual: CLAUDE.md:57 says Curl.Console does not pass one yet; EngineComposition.cs:190-192 passes contexts.Value for imap, pop3 and smtp.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

CLAUDE.md line 57 now says EngineComposition passes the security-context factory (BL-852 done).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. CLAUDE.md corrected; docs-only
