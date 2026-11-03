---
id: AF-0609
title: SmbReadResponse.TryGetData is named Try... but returns string? (null on success, error text on failure), not bool
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs:TryGetData:misleading-name
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0609 - SmbReadResponse.TryGetData is named Try... but returns string? (null on success, error text on failure), not bool

## Summary

Medium finding from the truthfulness auditor at `Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs:27`: SmbReadResponse.TryGetData is named Try... but returns string? (null on success, error text on failure), not bool.

## Evidence

Location: `Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs:27`

Sampled method (seed 985570). Signature: "public static string? TryGetData(byte[] received, out ReadOnlyMemory<byte> data)". Its doc: "<returns><see langword=\"null\" /> when the response is accepted, else the error message.</returns>". align-and-document's definition of aligned: "A method named Try... returns bool". Here a non-null value means failure, the opposite of how a Try result reads. The caller has to work around it: SmbFileTransfer.cs:140 "if (SmbReadResponse.TryGetData(read, out ReadOnlyMemory<byte> data) is { } refused)".

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitLibrary/SmbReadResponse.cs -Pattern 'public static \S+ TryGetData\('
```

- Expected: public static bool TryGetData(...), or a name that is not Try... for a method that returns the error message.
- Actual: public static string? TryGetData(byte[] received, out ReadOnlyMemory<byte> data)

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
