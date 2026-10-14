---
id: AF-0186
title: QuicTransportParameters.Encode's summary gives a parameter order the code does not write
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Quic.UnitLibrary/QuicTransportParameters.cs:Encode:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_1822.md
---
# AF-0186 - QuicTransportParameters.Encode's summary gives a parameter order the code does not write

## Summary

Low finding from the truthfulness auditor at `Curl.Quic.UnitLibrary/QuicTransportParameters.cs:102`: QuicTransportParameters.Encode's summary gives a parameter order the code does not write. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Quic.UnitLibrary/QuicTransportParameters.cs:102`

Doc comment (line 102): 'Writes the parameters ... in curl's order: the connection IDs, the stream and connection limits (bidirectional stream data before unidirectional, then initial_max_data, then the stream counts), the remaining integers, then version_information last.' Code (lines 110-127) writes 0x00,0x0f,0x10 (connection IDs), then 0x02 stateless_reset_token, 0x01 max_idle_timeout and 0x03 max_udp_payload_size (lines 115-116), and only then the stream and connection limits 0x05,0x06,0x07,0x04,0x08,0x09. So max_idle_timeout and max_udp_payload_size, two of the 'remaining integers', come before the limits, not after them. A reader who reorders the code to match the comment changes the bytes on the wire.

## Reproduction

Run from the repository root:

```powershell
((Select-String -Path Curl.Quic.UnitLibrary/QuicTransportParameters.cs -Pattern 'Write(?:Integer|Bytes)\(writer, (0x[0-9a-f]{2})' | ForEach-Object { $_.Matches[0].Groups[1].Value }) -join ',')
```

- Expected: An order matching the summary: the connection IDs, then 0x05,0x06,0x07,0x04,0x08,0x09, then the remaining integers (0x01, 0x03, ...), with 0x11 last. Or a summary rewritten to the code's order.
- Actual: 0x00,0x0f,0x10,0x02,0x01,0x03,0x05,0x06,0x07,0x04,0x08,0x09,0x0a,0x0b,0x0c,0x0e,0x0d,0x11

## Re-audits

- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran the reproduction: 0x00,0x0f,0x10,0x02,0x01,0x03,0x05,0x06,0x07,0x04,0x08,0x09,0x0a,0x0b,0x0c,0x0e,0x0d,0x11. The Encode summary (QuicTransportParameters.cs:102-109) gives the same order: connection IDs (original destination, initial source, retry source), stateless_reset_token, max_idle_timeout, max_udp_payload_size, bidi-local/bidi-remote/uni stream data, initial_max_data, stream counts, ack_delay_exponent, max_ack_delay, disable_active_migration, active_connection_id_limit, preferred_address, version_information last.

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
