---
id: AF-0710
title: Description_NamesTheOptionAndRfc6455 never checks that the description names RFC 6455
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs:Description_NamesTheOptionAndRfc6455:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0710 - Description_NamesTheOptionAndRfc6455 never checks that the description names RFC 6455

## Summary

Medium finding from the quality auditor at `Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs:27`: Description_NamesTheOptionAndRfc6455 never checks that the description names RFC 6455. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs:27`

Step 1. The body has two assertions: Assert.AreEqual("ws-repl", WebSocketReplExtension.Description.Name) and Assert.AreSame(WebSocketReplExtension.Option, Description.Options.Single()). The description's specification link (WebSocketReplExtension.cs:29, new Uri("https://www.rfc-editor.org/rfc/rfc6455")) is never read, so dropping or changing the RFC 6455 reference would not fail the test its name promises.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebSocketRepl.UnitTests/WebSocketReplExtensionTests.cs -Pattern 'Description_NamesTheOptionAndRfc6455' -Context 0,5
```

- Expected: The body asserts the description's RFC 6455 link (rfc6455).
- Actual: The body asserts only the name 'ws-repl' and the option; nothing mentions 6455.

## Re-audits

## Log

- 2026-11-12: filed proposed.
