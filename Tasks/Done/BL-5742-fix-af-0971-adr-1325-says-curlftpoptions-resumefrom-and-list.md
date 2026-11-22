---
id: BL-5742
title: Fix AF-0971: ADR-1325 says CurlFtpOptions.ResumeFrom and ListOnly sit with FTP, not on every protocol; the code applies them to every protocol
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5742 — Fix AF-0971: ADR-1325 says CurlFtpOptions.ResumeFrom and ListOnly sit with FTP, not on every protocol; the code applies them to every protocol

## Goal

The defect the audit office reported as AF-0971 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0971 (Low, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md:11`

Location: `Documentation/Planning/Decisions/ADR-1325-curl-api-ftp-resume-offset-and-names-only-listing-live-on-curlftpoptions.md:11`

ADR-1325 (Status: Accepted), Decision: "They sit with FTP, not on every protocol: the task asked for FTP, and a general resume offset for HTTP, FILE and SFTP can be added as its own option later". The code: Curl.Api/Options/Protocol/CurlFtpOptions.cs:97-99 says "Despite living on the FTP record, it is transfer-wide, as curl -C is: it applies to every protocol, so an HTTP request sends Range: bytes=N- (ADR-1328)", and ApplyTo (line 115-116) writes ResumeFrom and DirListOnly into the shared EasyTransferOptions, which every protocol reads. ListOnly's doc (line 109) says it also reaches SFTP and SCP. ADR-1328 records the transfer-wide behaviour, but ADR-1325 still stands as Accepted with no note that it is amended, so a reader of ADR-1325 alone would believe the offset is FTP-only.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1325-*.md,Curl.Api/Options/Protocol/CurlFtpOptions.cs -Pattern 'not on every|applies to every protocol'
```

- Expected: ADR-1325 states (or is amended to state) what the code does: the two members apply to every protocol.
- Actual: ADR-1325...md:11: ...They sit with FTP, not on every  and  CurlFtpOptions.cs:97: ... it is transfer-wide, as curl -C is: it applies to every protocol ...

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Amended ADR-1325 text to say the members are transfer-wide (ADR-1328); repro grep now gives 0 matches in the ADR.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-22: Doing -> Done. ADR-1325 now says ResumeFrom and ListOnly apply to every protocol
