---
id: AF-0674
title: ADR-0174 says a QUIC stream-state/final-size/flow-control error exits 7; QuicHandshakeFailures maps it to exit 56 after the handshake
auditor: truthfulness
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Documentation/Planning/Decisions/ADR-0174-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md:QuicStreamErrorExit:stale-adr
reproduction: none
task: BL-4862
tasks: BL-4862
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0674 - ADR-0174 says a QUIC stream-state/final-size/flow-control error exits 7; QuicHandshakeFailures maps it to exit 56 after the handshake

## Summary

High finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0174-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md:34`: ADR-0174 says a QUIC stream-state/final-size/flow-control error exits 7; QuicHandshakeFailures maps it to exit 56 after the handshake.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0174-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md:34`

ADR-0174 (Status: Accepted, no amendment) Decision item 3: 'a frame for a part the stream does not have, or for a client stream not opened, STREAM_STATE_ERROR; a changed or exceeded final size FINAL_SIZE_ERROR. The connection closes with that error; mapping it to curl's exit is BL-727's (for now it is the handshake's exit 7).' Code: QuicStreamSet.cs:200 throws QuicTransportException(QuicTransportErrorCode.StreamStateError, ...); QuicClientConnectionState.cs:218 'Fail(QuicHandshakeFailures.FromTransportError(error, IsComplete), error.ErrorCode)'; QuicHandshakeFailures.cs:46-48 'FromTransportError(...) => handshakeComplete ? ConnectionLost : new QuicHandshakeFailure(CurlExitCode.CouldntConnect, error.Message)'; line 22 'ConnectionLost { get; } = new(CurlExitCode.RecvError, ReceiveFailureMessage)' (exit 56). Stream frames arrive after the handshake, so the outcome is exit 56, not 7. Found by Find-AdrNumberMismatches.ps1 (exit-not-in-code: 'the Decision states exit 7; the code it names (QuicConnection) produces exit 56 and never 7').

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-0174-*.md -Pattern "handshake's exit 7"; Select-String -Path Curl.Quic.UnitLibrary/QuicHandshakeFailures.cs -Pattern 'handshakeComplete|ConnectionLost \{ get; \}'
```

- Expected: The ADR states exit 56 (or is amended) for a transport error after the handshake, matching FromTransportError.
- Actual: ADR-0174:34 '...mapping it to curl's exit is BL-727's (for now it is the handshake's exit 7).'; QuicHandshakeFailures.cs:22 'ConnectionLost { get; } = new(CurlExitCode.RecvError, ...)'; :46 'FromTransportError(QuicTransportException error, bool handshakeComplete) => handshakeComplete'

## Re-audits

## Log

- 2026-11-04: filed proposed.
