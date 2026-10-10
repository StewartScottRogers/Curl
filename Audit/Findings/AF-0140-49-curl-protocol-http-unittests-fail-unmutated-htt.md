---
id: AF-0140
title: 49 Curl.Protocol.Http.UnitTests fail unmutated: HttpResponseBodyReader counts every length-delimited/read-to-close body byte twice in BytesWritten
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:CopyFramedAsync:failing-test
reproduction: none
task: none
tasks:
found: 2026-10-09
found-at: 4653e86a969768525b597bcfec718242ead0959f
scorecard: 2026-10-09_1435.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0140 - 49 Curl.Protocol.Http.UnitTests fail unmutated: HttpResponseBodyReader counts every length-delimited/read-to-close body byte twice in BytesWritten

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:327`: 49 Curl.Protocol.Http.UnitTests fail unmutated: HttpResponseBodyReader counts every length-delimited/read-to-close body byte twice in BytesWritten. Reported by an auditor flagged unreliable in 2026-10-09_1435.md.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpResponseBodyReader.cs:327`

The mutation tester's baseline for Curl.Protocol.Http.UnitTests left out 49 tests that fail on the unmutated tree (HttpProtocolHandlerTests and HttpResponseBodyReaderTests, e.g. ExecuteAsync_Exchange_SendsTheRequestAndWritesHeadersThenBody, ExecuteAsync_BodyAtMaxFileSize_Succeeds, CopyAsync_Body_IsWrittenToTheOutput). CopyFramedAsync copies the body into 'MemoryStream held' through WriteAsync (lines 327 and 342) and then writes held.ToArray() to the output through WriteAsync again (lines 336 and 345). WriteAsync does 'BytesWritten += bytes.Length' (line 508), so each byte is counted twice: CopyAsync_Body_IsWrittenToTheOutput fails with 'Assert.AreEqual(body.Length, reader.BytesWritten) expected: 5 actual: 10' on every read-to-close and Content-Length row. The output also receives one write of the whole body instead of reads of at most ReadSize, so CopyAsync_OutputFailsOnALargeBody_ThrowsExit23WithCurlsReadSize gets 'passed 20000' where curl says 'passed 16384'. A user sees %{size_download} doubled and wrong write-failure text. AF-0056 and AF-0079 already cover the held-body symptom in 7 of these tests; the double count, and the 42 other failures it causes, are not covered by them.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Protocol.Http.UnitTests -c Release -nologo --filter "FullyQualifiedName~CopyAsync_Body_IsWrittenToTheOutput"
```

- Expected: Every row passes: BytesWritten equals the body length (5).
- Actual: Rows such as 'HTTP/1.1 read to close' fail: Assert.AreEqual(body.Length, reader.BytesWritten) expected: 5 actual: 10.

## Re-audits

## Log

- 2026-10-09: filed proposed.
- 2026-10-09: proposed -> accepted.
