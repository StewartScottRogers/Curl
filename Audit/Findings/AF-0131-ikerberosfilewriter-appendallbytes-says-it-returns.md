---
id: AF-0131
title: IKerberosFileWriter.AppendAllBytes says it returns false only when no file exists, but its implementation returns false on any I/O or access failure
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
key: truthfulness:Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs:AppendAllBytes:false-doc-comment
reproduction: none
task: BL-1881
tasks: BL-1881
found: 2026-10-09
found-at: 64e750b3931ea71536d942080142f37bfc4c9ccf
scorecard: 2026-10-09_0225.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0647.md, 2026-10-09_1435.md
---
# AF-0131 - IKerberosFileWriter.AppendAllBytes says it returns false only when no file exists, but its implementation returns false on any I/O or access failure

## Summary

Low finding from the truthfulness auditor at `Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs:13`: IKerberosFileWriter.AppendAllBytes says it returns false only when no file exists, but its implementation returns false on any I/O or access failure. Reported by an auditor flagged unreliable in 2026-10-09_0225.md.

## Evidence

Location: `Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs:13`

IKerberosFileWriter.cs:13: '<returns>true when the bytes were appended; false when no file exists there, which is not created.</returns>'. Curl.Console/KerberosDiskFileWriter.cs:24, the only implementation, returns false from 'catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)'. That covers a file it cannot open for writing, a sharing violation, and a write that fails part-way (for example a full disk) after some bytes have already been appended. The implementation's own summary admits 'one that does not exist or cannot be opened for writing is treated as absent'. The caller, CredentialCacheStore.cs:167-169, turns every false into KerberosFileException(KerberosFileError.NotFound), so the interface doc misleads a reader about what false means.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs -SimpleMatch 'when no file exists there'; Select-String -Path Curl.Console/KerberosDiskFileWriter.cs -SimpleMatch 'failure is IOException or UnauthorizedAccessException'
```

- Expected: The interface doc names every false case (missing, or cannot be opened or written), or the implementation returns false only for a missing file.
- Actual: IKerberosFileWriter.cs:13 says false only when no file exists; KerberosDiskFileWriter.cs:24 returns false for every IOException or UnauthorizedAccessException.

## Re-audits

- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the reproduction: 'when no file exists there' no longer matches IKerberosFileWriter.cs; KerberosDiskFileWriter.cs:24 still catches IOException or UnauthorizedAccessException. The interface's returns doc now says false when 'no file exists there (none is created), or the file cannot be opened or written', which matches the implementation.
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction: 'when no file exists there' no longer matches in Curl.Kerberos.UnitLibrary/IKerberosFileWriter.cs. KerberosDiskFileWriter.cs:24 still catches IOException or UnauthorizedAccessException, and the doc (lines 14-16) now says false means 'no file exists there (none is created), or the file cannot be opened or written'. That agrees with the code.

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
