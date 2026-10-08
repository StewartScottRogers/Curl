---
id: BL-1689
title: Fix AF-0070: KerberosKdcClient summary says every KRB-ERROR becomes a KerberosKdcException; KDC_ERR_PREAUTH_REQUIRED does not
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Kerberos.UnitLibrary]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1689 — Fix AF-0070: KerberosKdcClient summary says every KRB-ERROR becomes a KerberosKdcException; KDC_ERR_PREAUTH_REQUIRED does not

## Goal

The defect the audit office reported as AF-0070 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0070 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0070-kerberoskdcclient-summary-says-every-krb-error-bec.md`.

Location: `Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs:13`

Location: `Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs:13`

The class summary ends 'Every KRB-ERROR becomes a <see cref="KerberosKdcException" />.' GetInitialTicketAsync (lines 177-192) reads a KRB-ERROR and, when error.ErrorCode == PreAuthenticationRequired (line 184), does not throw: it reads the PA-ETYPE-INFO2 from the error and sends a second AS-REQ with PA-ENC-TIMESTAMP. The method's own doc says so, so this is imprecision in the class summary, not a hidden behaviour.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs -SimpleMatch 'error.ErrorCode != KerberosErrorMessage.PreAuthenticationRequired'
```

- Expected: No match, or a class summary that excepts the pre-authentication-required error.
- Actual: KerberosKdcClient.cs:184 matches: that KRB-ERROR leads to a retry, not an exception, while line 13 says every KRB-ERROR becomes one.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The class summary of `KerberosKdcClient` now excepts the `KDC_ERR_PREAUTH_REQUIRED` answering the first AS-REQ, which sends a second AS-REQ with `PA-ENC-TIMESTAMP` from the error's `PA-ETYPE-INFO2`. The code at the reproduction's line still matches (it is correct behaviour); the finding's expected result allows that, given a summary that excepts the error.
- Doc-comment-only change, no IL change: `dotnet build` of the solution is clean, and `Curl.Kerberos.UnitTests` fast tests pass (781). The rest of the fast suite was not rerun, since a comment cannot change any test outcome.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. KerberosKdcClient's class summary now excepts KDC_ERR_PREAUTH_REQUIRED, which retries with PA-ENC-TIMESTAMP instead of throwing
