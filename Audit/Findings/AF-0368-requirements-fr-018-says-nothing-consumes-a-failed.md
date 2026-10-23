---
id: AF-0368
title: Requirements FR-018 says nothing consumes a failed transfer's byte count until --write-out exists; --write-out exists and reads it
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Documentation/Product/Requirements.md:FR-018:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0368 - Requirements FR-018 says nothing consumes a failed transfer's byte count until --write-out exists; --write-out exists and reads it

## Summary

Low finding from the truthfulness auditor at `Documentation/Product/Requirements.md:51`: Requirements FR-018 says nothing consumes a failed transfer's byte count until --write-out exists; --write-out exists and reads it.

## Evidence

Location: `Documentation/Product/Requirements.md:51`

FR-018 (line 51): "... reports the number of bytes ... for `%{size_download}` and `%{size_upload}` once `--write-out` exists. `TransferResult.Failure` carries the count ...; nothing consumes it until `--write-out` exists." But --write-out is built: Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:137 maps size_download, and Curl.Output.UnitLibrary/WriteOutTransferInfo.cs:77 consumes the count: `long downloadSize = result.Report?.DownloadSize ?? result.BytesTransferred;`. The present-tense claim that nothing consumes the count, and that --write-out does not exist yet, is stale.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md, Curl.Output.UnitLibrary/WriteOutTransferInfo.cs -Pattern 'nothing consumes it until|\?\? result\.BytesTransferred' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Only WriteOutTransferInfo.cs:77 matches: FR-018 no longer says nothing consumes the count.
- Actual: Requirements.md:51 and WriteOutTransferInfo.cs:77: the requirement says nothing consumes the count, while write-out reads it.

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
