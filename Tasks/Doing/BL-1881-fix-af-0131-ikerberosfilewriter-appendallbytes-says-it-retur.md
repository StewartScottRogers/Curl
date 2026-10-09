---
id: BL-1881
title: Fix AF-0131: IKerberosFileWriter.AppendAllBytes says it returns false only when no file exists, but its implementation returns false on any I/O or access failure
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Kerberos.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1881 — Fix AF-0131: IKerberosFileWriter.AppendAllBytes says it returns false only when no file exists, but its implementation returns false on any I/O or access failure

## Goal

The defect the audit office reported as AF-0131 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0131 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0131-ikerberosfilewriter-appendallbytes-says-it-returns.md`.

Location: `Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs:13`

Location: `Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs:13`

IKerberosFileWriter.cs:13: '<returns>true when the bytes were appended; false when no file exists there, which is not created.</returns>'. Curl.Console/KerberosDiskFileWriter.cs:24, the only implementation, returns false from 'catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)'. That covers a file it cannot open for writing, a sharing violation, and a write that fails part-way (for example a full disk) after some bytes have already been appended. The implementation's own summary admits 'one that does not exist or cannot be opened for writing is treated as absent'. The caller, CredentialCacheStore.cs:167-169, turns every false into KerberosFileException(KerberosFileError.NotFound), so the interface doc misleads a reader about what false means.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs -SimpleMatch 'when no file exists there'; Select-String -Path Curl.Console/KerberosDiskFileWriter.cs -SimpleMatch 'failure is IOException or UnauthorizedAccessException'
```

- Expected: The interface doc names every false case (missing, or cannot be opened or written), or the implementation returns false only for a missing file.
- Actual: IKerberosFileWriter.cs:13 says false only when no file exists; KerberosDiskFileWriter.cs:24 returns false for every IOException or UnauthorizedAccessException.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
