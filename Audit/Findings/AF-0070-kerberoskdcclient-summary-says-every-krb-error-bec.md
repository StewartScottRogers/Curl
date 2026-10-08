---
id: AF-0070
title: KerberosKdcClient summary says every KRB-ERROR becomes a KerberosKdcException; KDC_ERR_PREAUTH_REQUIRED does not
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs:KerberosKdcClient:false-doc-comment
reproduction: none
task: BL-1689
tasks: BL-1689
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0070 - KerberosKdcClient summary says every KRB-ERROR becomes a KerberosKdcException; KDC_ERR_PREAUTH_REQUIRED does not

## Summary

Low finding from the truthfulness auditor at `Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs:13`: KerberosKdcClient summary says every KRB-ERROR becomes a KerberosKdcException; KDC_ERR_PREAUTH_REQUIRED does not. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs:13`

The class summary ends 'Every KRB-ERROR becomes a <see cref="KerberosKdcException" />.' GetInitialTicketAsync (lines 177-192) reads a KRB-ERROR and, when error.ErrorCode == PreAuthenticationRequired (line 184), does not throw: it reads the PA-ETYPE-INFO2 from the error and sends a second AS-REQ with PA-ENC-TIMESTAMP. The method's own doc says so, so this is imprecision in the class summary, not a hidden behaviour.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitLibrary/KerberosKdcClient.cs -SimpleMatch 'error.ErrorCode != KerberosErrorMessage.PreAuthenticationRequired'
```

- Expected: No match, or a class summary that excepts the pre-authentication-required error.
- Actual: KerberosKdcClient.cs:184 matches: that KRB-ERROR leads to a retry, not an exception, while line 13 says every KRB-ERROR becomes one.

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | The reproduction still finds KerberosKdcClient.cs:186 'if (error.ErrorCode != KerberosErrorMessage.PreAuthenticationRequired)', but the class summary (lines 13-16) now reads 'Every KRB-ERROR becomes a KerberosKdcException, except the KDC_ERR_PREAUTH_REQUIRED answering the first AS-REQ, which instead sends a second AS-REQ with PA-ENC-TIMESTAMP ...'. The doc and the code agree.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
