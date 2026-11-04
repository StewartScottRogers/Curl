---
id: BL-4791
title: Fix AF-0618: Surviving mutant: a CONNECT reply whose first byte is NUL can escape the NUL-byte refusal without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4791 — Fix AF-0618: Surviving mutant: a CONNECT reply whose first byte is NUL can escape the NUL-byte refusal without a test failing

## Goal

The defect the audit office reported as AF-0618 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0618 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0618-surviving-mutant-a-connect-reply-whose-first-byte.md`.

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231`

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231`

Mutant 'if (header.IndexOf(0, lineStart) >= 0)' -> '> 0' survived. IndexOf returns an absolute index, so the mutant differs only when the NUL sits at index 0, the reply's first byte. That reply then skips NulByteReply and is parsed as a status line, so the proxy tunnel's exit code and message change. No HttpProxyTunnelTests case starts the reply with a NUL.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231:>= -Member ReplyAfterLineEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
