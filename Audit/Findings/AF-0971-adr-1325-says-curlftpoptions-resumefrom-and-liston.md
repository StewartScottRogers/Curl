---
id: AF-0971
title: ADR-1325 says CurlFtpOptions.ResumeFrom and ListOnly sit with FTP, not on every protocol; the code applies them to every protocol
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: truthfulness:Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md:CurlFtpOptions.ResumeFrom:stale-adr
reproduction: none
task: BL-5742
tasks: BL-5742
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
---
# AF-0971 - ADR-1325 says CurlFtpOptions.ResumeFrom and ListOnly sit with FTP, not on every protocol; the code applies them to every protocol

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md:11`: ADR-1325 says CurlFtpOptions.ResumeFrom and ListOnly sit with FTP, not on every protocol; the code applies them to every protocol. Reported by an auditor flagged unreliable in 2026-11-22_0210.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md:11`

ADR-1325 (Status: Accepted), Decision: "They sit with FTP, not on every protocol: the task asked for FTP, and a general resume offset for HTTP, FILE and SFTP can be added as its own option later". The code: Curl.Api/Options/Protocol/CurlFtpOptions.cs:97-99 says "Despite living on the FTP record, it is transfer-wide, as curl -C is: it applies to every protocol, so an HTTP request sends Range: bytes=N- (ADR-1328)", and ApplyTo (line 115-116) writes ResumeFrom and DirListOnly into the shared EasyTransferOptions, which every protocol reads. ListOnly's doc (line 109) says it also reaches SFTP and SCP. ADR-1328 records the transfer-wide behaviour, but ADR-1325 still stands as Accepted with no note that it is amended, so a reader of ADR-1325 alone would believe the offset is FTP-only.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1325-*.md,Curl.Api/Options/Protocol/CurlFtpOptions.cs -Pattern 'not on every|applies to every protocol'
```

- Expected: ADR-1325 states (or is amended to state) what the code does: the two members apply to every protocol.
- Actual: ADR-1325...md:11: ...They sit with FTP, not on every  and  CurlFtpOptions.cs:97: ... it is transfer-wide, as curl -C is: it applies to every protocol ...

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the reproduction: no 'not on every' match in ADR-1325. Its Decision now says the two members 'are transfer-wide ... apply to every protocol'. The only match is CurlFtpOptions.cs:97 'it applies to every protocol', which agrees with the code (ApplyTo writes the shared EasyTransferOptions).

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
