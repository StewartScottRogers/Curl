---
id: BL-4862
title: Fix AF-0674: ADR-0174 says a QUIC stream-state/final-size/flow-control error exits 7; QuicHandshakeFailures maps it to exit 56 after the handshake
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4862 — Fix AF-0674: ADR-0174 says a QUIC stream-state/final-size/flow-control error exits 7; QuicHandshakeFailures maps it to exit 56 after the handshake

## Goal

The defect the audit office reported as AF-0674 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0674 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0674-adr-0174-says-a-quic-stream-state-final-size-flow.md`.

Location: `Documentation/Planning/Decisions/ADR-0174-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md:34`

Location: `Documentation/Planning/Decisions/ADR-0174-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md:34`

ADR-0174 (Status: Accepted, no amendment) Decision item 3: 'a frame for a part the stream does not have, or for a client stream not opened, STREAM_STATE_ERROR; a changed or exceeded final size FINAL_SIZE_ERROR. The connection closes with that error; mapping it to curl's exit is BL-727's (for now it is the handshake's exit 7).' Code: QuicStreamSet.cs:200 throws QuicTransportException(QuicTransportErrorCode.StreamStateError, ...); QuicClientConnectionState.cs:218 'Fail(QuicHandshakeFailures.FromTransportError(error, IsComplete), error.ErrorCode)'; QuicHandshakeFailures.cs:46-48 'FromTransportError(...) => handshakeComplete ? ConnectionLost : new QuicHandshakeFailure(CurlExitCode.CouldntConnect, error.Message)'; line 22 'ConnectionLost { get; } = new(CurlExitCode.RecvError, ReceiveFailureMessage)' (exit 56). Stream frames arrive after the handshake, so the outcome is exit 56, not 7. Found by Find-AdrNumberMismatches.ps1 (exit-not-in-code: 'the Decision states exit 7; the code it names (QuicConnection) produces exit 56 and never 7').

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0174-*.md -Pattern "handshake's exit 7"; Select-String -Path Curl.Quic.UnitLibrary/QuicHandshakeFailures.cs -Pattern 'handshakeComplete|ConnectionLost \{ get; \}'
```

- Expected: The ADR states exit 56 (or is amended) for a transport error after the handshake, matching FromTransportError.
- Actual: ADR-0174:34 '...mapping it to curl's exit is BL-727's (for now it is the handshake's exit 7).'; QuicHandshakeFailures.cs:22 'ConnectionLost { get; } = new(CurlExitCode.RecvError, ...)'; :46 'FromTransportError(QuicTransportException error, bool handshakeComplete) => handshakeComplete'

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

ADR-0174 Decision item 3 amended to name FromTransportError: exit 7 during the handshake, exit 56 after. Docs-only; no code changed, so build and tests were not rerun.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. ADR-0174 amended to state exit 56 after the handshake
