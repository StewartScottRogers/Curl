---
id: BL-4773
title: Fix AF-0609: SmbReadResponse.TryGetData is named Try... but returns string? (null on success, error text on failure), not bool
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Original.Protocol.Smb.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4773 — Fix AF-0609: SmbReadResponse.TryGetData is named Try... but returns string? (null on success, error text on failure), not bool

## Goal

The defect the audit office reported as AF-0609 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0609 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0609-smbreadresponse-trygetdata-is-named-try-but-return.md`.

Location: `Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs:27`

Location: `Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs:27`

Sampled method (seed 985570). Signature: "public static string? TryGetData(byte[] received, out ReadOnlyMemory<byte> data)". Its doc: "<returns><see langword=\"null\" /> when the response is accepted, else the error message.</returns>". align-and-document's definition of aligned: "A method named Try... returns bool". Here a non-null value means failure, the opposite of how a Try result reads. The caller has to work around it: SmbFileTransfer.cs:140 "if (SmbReadResponse.TryGetData(read, out ReadOnlyMemory<byte> data) is { } refused)".

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs -Pattern 'public static \S+ TryGetData\('
```

- Expected: public static bool TryGetData(...), or a name that is not Try... for a method that returns the error message.
- Actual: public static string? TryGetData(byte[] received, out ReadOnlyMemory<byte> data)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
