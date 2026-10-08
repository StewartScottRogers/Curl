---
id: BL-1764
title: Fix AF-0086: Requirements FR-085 cites a test that does not exist and states one reset message where the code has several
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-08
completed:
---
# BL-1764 — Fix AF-0086: Requirements FR-085 cites a test that does not exist and states one reset message where the code has several

## Goal

The defect the audit office reported as AF-0086 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0086 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0086-requirements-fr-085-cites-a-test-that-does-not-exi.md`.

Location: `Documentation/Product/Requirements.md:237`

Location: `Documentation/Product/Requirements.md:237`

FR-085 says: 'Built; shown by `HttpProtocolHandlerTests.ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage`, which pins both messages.' No test in any *.UnitTests project has that name. The exit-55 send-failure tests are Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Timeouts.cs:329 ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheWinsockWords (Windows only, DataRows 'Send failure: Connection was reset' and 'Send failure: Connection was aborted'), :343 ExecuteAsync_SocketErrorFailsASend_FailsWithExit55AndTheSocketErrorsOwnWords (off Windows, 'Send failure: ' + SocketException.Message) and :351 ExecuteAsync_SendFailsWithNoSocketError_FailsWithExit55AndCurlsGenericText. FR-085 also says the message is 'Send failure: Connection was reset' when the peer resets the connection. The code also produces 'Connection was aborted', and off Windows it uses the OS's own socket-error words. An agent following the requirement looks for a test that is not there.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage').Count; (Select-String -Path Documentation/Product/Requirements.md -SimpleMatch 'ExecuteAsync_ConnectionFailsASend_FailsWithExit55AndTheMeasuredMessage').LineNumber
```

- Expected: Either the cited test exists (count 1 or more) or Requirements.md no longer cites it (no line number).
- Actual: 0, then 237: Requirements.md line 237 cites a test that exists nowhere.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
