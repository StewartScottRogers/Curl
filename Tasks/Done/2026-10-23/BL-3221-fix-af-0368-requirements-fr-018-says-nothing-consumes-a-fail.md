---
id: BL-3221
title: Fix AF-0368: Requirements FR-018 says nothing consumes a failed transfer's byte count until --write-out exists; --write-out exists and reads it
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3221 — Fix AF-0368: Requirements FR-018 says nothing consumes a failed transfer's byte count until --write-out exists; --write-out exists and reads it

## Goal

The defect the audit office reported as AF-0368 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0368 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0368-requirements-fr-018-says-nothing-consumes-a-failed.md`.

Location: `Documentation/Product/Requirements.md:51`

Location: `Documentation/Product/Requirements.md:51`

FR-018 (line 51): "... reports the number of bytes ... for `%{size_download}` and `%{size_upload}` once `--write-out` exists. `TransferResult.Failure` carries the count ...; nothing consumes it until `--write-out` exists." But --write-out is built: Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:137 maps size_download, and Curl.Output.UnitLibrary/WriteOutTransferInfo.cs:77 consumes the count: `long downloadSize = result.Report?.DownloadSize ?? result.BytesTransferred;`. The present-tense claim that nothing consumes the count, and that --write-out does not exist yet, is stale.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Output.UnitLibrary/WriteOutTransferInfo.cs -Pattern 'nothing consumes it until|\?\? result\.BytesTransferred' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only WriteOutTransferInfo.cs:77 matches: FR-018 no longer says nothing consumes the count.
- Actual: Requirements.md:51 and WriteOutTransferInfo.cs:77: the requirement says nothing consumes the count, while write-out reads it.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Reworded FR-018 in Requirements.md: dropped the stale "once --write-out exists" and "nothing consumes it" claims; it now says --write-out reads the count (WriteOutTransferInfo). Reproduction matches only WriteOutTransferInfo.cs:77. Build clean, fast tests green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-23: Doing -> Done. FR-018 no longer claims nothing consumes the failed-transfer byte count; it says --write-out reads it
