---
id: BL-3322
title: CURLAUTH_ANY and CURLAUTH_ANYSAFE on Windows leave the SSPI failure text in the error buffer as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferInfoFromReport.Auth.cs, Curl.Engine.UnitLibrary/NegotiateFailureRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs, Curl.Engine.UnitTests/NegotiateFailureRecordingTransferEventsTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3322 — CURLAUTH_ANY and CURLAUTH_ANYSAFE on Windows leave the SSPI failure text in the error buffer as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

authall-any and authall-anysafe (/authall, u:p): the Schannel/SSPI libcurl returns 0 and leaves "InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package" in CURLOPT_ERRORBUFFER; Curl's answer differs (see the report). Windows only: pin the other platforms' answer separately.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- touches widened from TransferInfoFromReport.Auth.cs to the files the change landed in (no other Doing task names them): a new NegotiateFailureRecordingTransferEvents records the first SSPI failure line a Negotiate step reports, ManagedEasyTransfer.Parsed.cs and SetUp.cs wire it, and ReportOn puts the line in ErrorText of a successful result and Negotiate in AuthSchemePicked when none was picked (libcurl keeps Negotiate for CURLINFO_HTTPAUTH_USED). The GSS-API build reports its line with infof, so nothing matches there; the unit tests feed the events directly and are platform-neutral.
- Checked: LibcurlEasyDifferentialTests report (Git ucrt64 libcurl 8.22.0) no longer lists authall-any or authall-anysafe; Measure-CodeQuality.ps1 Curl.Engine.UnitLibrary 0 failing, 100% line and branch.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. authall-any and authall-anysafe on Windows now leave the SSPI failure in the error buffer and report Negotiate as HTTPAUTH_USED as libcurl does
