---
id: BL-3473
title: Fix AF-0425: DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3473 — Fix AF-0425: DarwinFastOpenConnect.TryConnect, documented to return the socket or null, throws SocketException when dup fails

## Goal

The defect the audit office reported as AF-0425 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0425 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0425-darwinfastopenconnect-tryconnect-documented-to-ret.md`.

Location: `Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:70`

Location: `Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs:70`

TryConnect (line 41) is documented '<returns>The connected socket, or <see langword="null" />.</returns>' and its summary says connectx's refusal returns null so the caller connects as usual. No <exception> tag is given. Yet line 70: 'throw new SocketException((int)SocketError.TooManyOpenSockets);' when Duplicate(descriptor) < 0 (EMFILE). A Try-named method that throws, with the throw absent from its doc comment.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitLibrary/DarwinFastOpenConnect.cs -Pattern 'Socket\? TryConnect', 'throw new SocketException', '<exception' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Either no throw inside TryConnect, or an <exception cref="SocketException"> line documenting it.
- Actual: DarwinFastOpenConnect.cs:41 (TryConnect) and DarwinFastOpenConnect.cs:70 (throw new SocketException); no <exception> tag.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
