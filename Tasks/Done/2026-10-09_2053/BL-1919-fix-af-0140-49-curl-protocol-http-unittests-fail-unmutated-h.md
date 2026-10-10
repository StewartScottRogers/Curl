---
id: BL-1919
title: Fix AF-0140: 49 Curl.Protocol.Http.UnitTests fail unmutated: HttpResponseBodyReader counts every length-delimited/read-to-close body byte twice in BytesWritten
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-10
completed: 2026-10-09
---
# BL-1919 — Fix AF-0140: 49 Curl.Protocol.Http.UnitTests fail unmutated: HttpResponseBodyReader counts every length-delimited/read-to-close body byte twice in BytesWritten

## Goal

The defect the audit office reported as AF-0140 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0140 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0140-49-curl-protocol-http-unittests-fail-unmutated-htt.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:327`

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:327`

The mutation tester's baseline for Curl.Protocol.Http.UnitTests left out 49 tests that fail on the unmutated tree (HttpProtocolHandlerTests and HttpResponseBodyReaderTests, e.g. ExecuteAsync_Exchange_SendsTheRequestAndWritesHeadersThenBody, ExecuteAsync_BodyAtMaxFileSize_Succeeds, CopyAsync_Body_IsWrittenToTheOutput). CopyFramedAsync copies the body into 'MemoryStream held' through WriteAsync (lines 327 and 342) and then writes held.ToArray() to the output through WriteAsync again (lines 336 and 345). WriteAsync does 'BytesWritten += bytes.Length' (line 508), so each byte is counted twice: CopyAsync_Body_IsWrittenToTheOutput fails with 'Assert.AreEqual(body.Length, reader.BytesWritten) expected: 5 actual: 10' on every read-to-close and Content-Length row. The output also receives one write of the whole body instead of reads of at most ReadSize, so CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize gets 'passed 20000' where curl says 'passed 16384'. A user sees %{size_download} doubled and wrong write-failure text. AF-0056 and AF-0079 already cover the held-body symptom in 7 of these tests; the double count, and the 42 other failures it causes, are not covered by them.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~CopyAsync_Body_IsWrittenToTheOutput"
```

- Expected: Every row passes: BytesWritten equals the body length (5).
- Actual: Rows such as 'HTTP/1.1 read to close' fail: Assert.AreEqual(body.Length, reader.BytesWritten) expected: 5 actual: 10.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-09 (lane 1): No code change was needed. The finding's reproduction already gives the expected result on this tree. `CopyAsync_Body_IsWrittenToTheOutput`, `CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize`, `ExecuteAsync_BodyAtMaxFileSize_Succeeds` and `ExecuteAsync_Exchange_SendsTheRequestAndWritesHeadersThenBody` all pass (10 of 10 rows), and the whole of Curl.Protocol.Http.UnitTests passes (1980 passed, 18 skipped, 0 failed). `CopyFramedAsync` writes the prefix, then each read of at most ReadSize, straight to the output through `WriteAsync` once, so `BytesWritten` counts each byte once.
- The `MemoryStream held` code the finding quotes appears nowhere in this branch's history (`git log -S "MemoryStream held"` finds nothing). The audited tree most likely carried a defect planted by audit-seeder, and the auditor reported it as a real finding. The audit office should check AF-0140 against that run's planted-defect manifest. A re-audit can close it.

## Log

- 2026-10-10: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. AF-0140 reproduction passes; no code change needed (see Notes)
