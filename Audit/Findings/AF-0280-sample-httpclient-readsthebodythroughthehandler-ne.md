---
id: AF-0280
title: Sample_HttpClient_ReadsTheBodyThroughTheHandler never checks the body it names
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Api.UnitTests/Readme/ReadmeTests.cs:Sample_HttpClient_ReadsTheBodyThroughTheHandler:no-assertion
reproduction: none
task: BL-2807
tasks: BL-2807
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0280 - Sample_HttpClient_ReadsTheBodyThroughTheHandler never checks the body it names

## Summary

Low finding from the quality auditor at `Curl.Api.UnitTests/Readme/ReadmeTests.cs:214`: Sample_HttpClient_ReadsTheBodyThroughTheHandler never checks the body it names. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Api.UnitTests/Readme/ReadmeTests.cs:214`

The test awaits ReadmeSamples.UseWithHttpClientAsync(client, CancellationToken.None), which writes http.GetStringAsync(...)'s text to Console. The scripted transfer's body is "hello" (ReadmeTests.cs:47), but nothing compares the text, so a handler returning an empty or wrong body passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Readme/ReadmeTests.cs -Pattern 'Sample_HttpClient_ReadsTheBodyThroughTheHandler' -Context 0,6
```

- Expected: The test asserts the body read through CurlHttpMessageHandler is "hello".
- Actual: The method body holds no Assert call.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
