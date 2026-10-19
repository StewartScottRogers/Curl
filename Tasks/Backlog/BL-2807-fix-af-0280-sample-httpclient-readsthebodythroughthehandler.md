---
id: BL-2807
title: Fix AF-0280: Sample_HttpClient_ReadsTheBodyThroughTheHandler never checks the body it names
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2807 — Fix AF-0280: Sample_HttpClient_ReadsTheBodyThroughTheHandler never checks the body it names

## Goal

The defect the audit office reported as AF-0280 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0280 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0280-sample-httpclient-readsthebodythroughthehandler-ne.md`.

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:214`

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:214`

The test awaits ReadmeSamples.UseWithHttpClientAsync(client, CancellationToken.None), which writes http.GetStringAsync(...)'s text to Console. The scripted transfer's body is "hello" (ReadmeTests.cs:47), but nothing compares the text, so a handler returning an empty or wrong body passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_HttpClient_ReadsTheBodyThroughTheHandler' -Context 0,6
```

- Expected: The test asserts the body read through CurlHttpMessageHandler is "hello".
- Actual: The method body holds no Assert call.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
