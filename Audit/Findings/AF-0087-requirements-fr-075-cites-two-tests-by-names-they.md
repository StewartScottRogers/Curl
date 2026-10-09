---
id: AF-0087
title: Requirements FR-075 cites two tests by names they no longer have
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
key: truthfulness:Documentation/Product/Requirements.md:FR-075:false-statement
reproduction: none
task: BL-1765
tasks: BL-1765
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-08_2315.md, 2026-10-09_0225.md
---
# AF-0087 - Requirements FR-075 cites two tests by names they no longer have

## Summary

Low finding from the truthfulness auditor at `Documentation/Product/Requirements.md:227`: Requirements FR-075 cites two tests by names they no longer have. Reported by an auditor flagged unreliable in 2026-10-08_0748.md.

## Evidence

Location: `Documentation/Product/Requirements.md:227`

FR-075 says it is shown by `HttpResponseHeadReaderTests.ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError` and `HttpResponseBodyReaderTests.CopyAsync_PeerResetsMidBody_ThrowsExit56AfterWritingWhatArrived`. Neither method exists. The tests were split by platform: Curl.Protocol.Http.UnitTests/HttpResponseHeadReaderTests.cs:383 ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvErrorWithTheWinsockWords and :397 ...WithTheSocketErrorsOwnWords, and HttpResponseBodyReaderTests.cs:156 CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheWinsockWordsAfterWritingWhatArrived and :168 ...WithTheSocketErrorsOwnWordsAfterWritingWhatArrived. FR-075's 'Recv failure: Connection was reset' wording is also only the Windows answer. The cited names are prefixes of the real ones, so a reader can still find them.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -Pattern 'ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError\(|CopyAsync_PeerResetsMidBody_ThrowsExit56AfterWritingWhatArrived\(').Count; (Select-String -Path Documentation/Product/Requirements.md -SimpleMatch 'ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError`').LineNumber
```

- Expected: Either both cited methods exist (count 2) or Requirements.md cites the current names (no line number).
- Actual: 0, then 227.

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | The old-name count is 0 and Requirements.md no longer cites the old name (no line number). FR-075 now cites ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvErrorWithTheWinsockWords/...SocketErrorsOwnWords and CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheWinsockWordsAfterWritingWhatArrived/...SocketErrorsOwnWords..., and all four exist.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the reproduction. The test search found 0 matches, and Requirements.md no longer contains the old name 'ReadAsync_ConnectionResetWhileReadingTheHead_ReturnsRecvError`' (no line number). FR-075 (Requirements.md:227) now cites ...ReturnsRecvErrorWithTheWinsockWords, ...ReturnsRecvErrorWithTheSocketErrorsOwnWords, CopyAsync_PeerResetsMidBody_ThrowsExit56WithTheWinsockWordsAfterWritingWhatArrived and ...WithTheSocketErrorsOwnWordsAfterWritingWhatArrived, and all four exist in HttpResponseHeadReaderTests.cs and HttpResponseBodyReaderTests.cs.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
