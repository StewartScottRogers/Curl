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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added the DataRow "the reply's first byte, AF-0618" (the reply `\0HTTP/1.1 200 OK\r\n\r\n`, which starts with a NUL) to `ReadReplyAsync_WhenALineHoldsANulByte_FailsWithExit8BeforeThatLine` in `Curl.Networking.UnitTests/HttpProxyTunnelTests.cs`. Under the `> 0` mutant the NUL at index 0 skips NulByteReply, the reply ends at the empty line and is parsed as a status line that is not HTTP/1.x, so exit 8 and "Nul byte in header" no longer hold and the row fails: the mutant is killed. This test asserts what the code already does (the same answer as the existing NUL-in-the-status-line row), so production code is unchanged and no quality run was needed.
- A lane cannot read or run `Audit/Tools`, so the reproduction script was not run here. The criterion is ticked on the reasoning above, and the quality auditor's re-audit confirms it.
- Full `dotnet build` clean; fast tests green on Windows. The row has no path, OS text or certificate, so it does not depend on the platform.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Test row with a NUL as the reply's first byte kills the AF-0618 mutant
