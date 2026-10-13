---
id: BL-2145
title: Fix AF-0186: QuicTransportParameters.Encode's summary gives a parameter order the code does not write
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Quic.UnitLibrary]
requirement: none
created: 2026-10-13
completed:
---
# BL-2145 — Fix AF-0186: QuicTransportParameters.Encode's summary gives a parameter order the code does not write

## Goal

The defect the audit office reported as AF-0186 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0186 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0186-quictransportparameters-encode-s-summary-gives-a-p.md`.

Location: `Curl.Quic.UnitLibrary/QuicTransportParameters.cs:102`

Location: `Curl.Quic.UnitLibrary/QuicTransportParameters.cs:102`

Doc comment (line 102): 'Writes the parameters ... in curl's order: the connection IDs, the stream and connection limits (bidirectional stream data before unidirectional, then initial_max_data, then the stream counts), the remaining integers, then version_information last.' Code (lines 110-127) writes 0x00,0x0f,0x10 (connection IDs), then 0x02 stateless_reset_token, 0x01 max_idle_timeout and 0x03 max_udp_payload_size (lines 115-116), and only then the stream and connection limits 0x05,0x06,0x07,0x04,0x08,0x09. So max_idle_timeout and max_udp_payload_size, two of the 'remaining integers', come before the limits, not after them. A reader who reorders the code to match the comment changes the bytes on the wire.

Reproduction, from the finding:

Run from the repository root:

```powershell
((Select-String -Path Curl.Quic.UnitLibrary/QuicTransportParameters.cs -Pattern 'Write(?:Integer|Bytes)\(writer, (0x[0-9a-f]{2})' | ForEach-Object { $_.Matches[0].Groups[1].Value }) -join ',')
```

- Expected: An order matching the summary: the connection IDs, then 0x05,0x06,0x07,0x04,0x08,0x09, then the remaining integers (0x01, 0x03, ...), with 0x11 last. Or a summary rewritten to the code's order.
- Actual: 0x00,0x0f,0x10,0x02,0x01,0x03,0x05,0x06,0x07,0x04,0x08,0x09,0x0a,0x0b,0x0c,0x0e,0x0d,0x11

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
