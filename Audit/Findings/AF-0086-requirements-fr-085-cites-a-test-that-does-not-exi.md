---
id: AF-0086
title: Requirements FR-085 cites a test that does not exist and states one reset message where the code has several
auditor: truthfulness
severity: Medium
status: accepted
reason:
key: truthfulness:Documentation/Product/Requirements.md:FR-085:false-statement
reproduction: none
task: BL-1764
tasks: BL-1764
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0086 - Requirements FR-085 cites a test that does not exist and states one reset message where the code has several

## Summary

Medium finding from the truthfulness auditor at `Documentation/Product/Requirements.md:237`: Requirements FR-085 cites a test that does not exist and states one reset message where the code has several. Reported by an auditor flagged unreliable in 2026-10-08_0748.md.

## Evidence

Location: `Documentation/Product/Requirements.md:237`

FR-085 says: 'Built; shown by `HttpProtocolHandlerTests.ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage`, which pins both messages.' No test in any *.UnitTests project has that name. The exit-55 send-failure tests are Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Timeouts.cs:329 ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheWinsockWords (Windows only, DataRows 'Send failure: Connection was reset' and 'Send failure: Connection was aborted'), :343 ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheSocketErrorsOwnWords (off Windows, 'Send failure: ' + SocketException.Message) and :351 ExecuteAsync_SendFailsWithNoSocketError_FailsWithExit55AndCurlsGenericText. FR-085 also says the message is 'Send failure: Connection was reset' when the peer resets the connection. The code also produces 'Connection was aborted', and off Windows it uses the OS's own socket-error words. An agent following the requirement looks for a test that is not there.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage').Count; (Select-String -Path Documentation/Product/Requirements.md -SimpleMatch 'ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage').LineNumber
```

- Expected: Either the cited test exists (count 1 or more) or Requirements.md no longer cites it (no line number).
- Actual: 0, then 237: Requirements.md line 237 cites a test that exists nowhere.

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | The test-name count is 0 and Requirements.md no longer cites it (no line number). FR-085 now gives the Winsock words on Windows, the OS socket-error text elsewhere and 'Failed sending data to the peer' with no socket error. It cites ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheWinsockWords, ..._AndTheSocketErrorsOwnWords and ExecuteAsync_SendFailsWithNoSocketError_FailsWithExit55AndCurlsGenericText, and all three exist in HttpProtocolHandlerTests.Timeouts.cs.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
