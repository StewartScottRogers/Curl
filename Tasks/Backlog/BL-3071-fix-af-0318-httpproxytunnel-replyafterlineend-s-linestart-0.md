---
id: BL-3071
title: Fix AF-0318: HttpProxyTunnel.ReplyAfterLineEnd's lineStart == 0 can become != 0 with no test failing, so a CONNECT reply whose first line is not HTTP/ no longer fails at once with exit 43
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3071 — Fix AF-0318: HttpProxyTunnel.ReplyAfterLineEnd's lineStart == 0 can become != 0 with no test failing, so a CONNECT reply whose first line is not HTTP/ no longer fails at once with exit 43

## Goal

The defect the audit office reported as AF-0318 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0318 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0318-httpproxytunnel-replyafterlineend-s-linestart-0-ca.md`.

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221`

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221`

Sampled mutant (seed 0) survived: 'if (lineStart == 0 && !StartsWithHttpVersion(header)) return HttpProxyTunnelReply.Failed(InvalidResponseHeader) with { FailureExitCode = CurlExitCode.BadFunctionArgument };' with == turned into !=. This is the measured curl 8.21.0 behaviour (upstream test750, BL-1857): exit 43 'Invalid response header' once the first line ends. Under the mutant, the first line is never checked. A one-line garbage reply followed by an empty line goes to ParseReply instead of the exit 43 failure. A garbage first line from a proxy that then stalls waits for more bytes instead of failing. Either way the exit code and stderr differ, and the Networking twin's tests did not notice.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221:== -Member ReplyAfterLineEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
