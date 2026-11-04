---
id: AF-0547
title: Lib1576_UploadsWithTheCustomMethod never checks the custom method, and checks the read callback only for null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:Lib1576_UploadsWithTheCustomMethodAndAnswersItsCode:weak-assertion
reproduction: none
task: BL-4695
tasks: BL-4695
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
---
# AF-0547 - Lib1576_UploadsWithTheCustomMethod never checks the custom method, and checks the read callback only for null

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:362`: Lib1576_UploadsWithTheCustomMethod never checks the custom method, and checks the read callback only for null. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs:362`

The body asserts the exit code, 'Assert.IsNotNull(read);' and the follow mode. Nothing checks the custom request method the name promises, and any read callback passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1564To1588Tests.cs -Pattern 'Lib1576_UploadsWithTheCustomMethod' -Context 0,12
```

- Expected: An assertion on the custom request method and on what the read callback uploads.
- Actual: Assert.IsNotNull(read); is the only check of the upload.

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Lib1576_UploadsWithTheCustomMethodAndAnswersItsCode now asserts Assert.AreEqual("CURL", method) and calls read(buffer), asserting the uploaded length and bytes.
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Lib1576 test now asserts Assert.AreEqual("CURL", method), calls read(buffer) and checks Assert.AreEqual(Lib1576.Body.Length, uploaded) and the bytes.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
