---
id: BL-3287
title: Write Schannel's SEC_E_ILLEGAL_MESSAGE alert text and the --tls-max 1.2 SEC_E_UNSUPPORTED_FUNCTION failure
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3287 — Write Schannel's SEC_E_ILLEGAL_MESSAGE alert text and the --tls-max 1.2 SEC_E_UNSUPPORTED_FUNCTION failure

## Goal

A handshake the server ends with a fatal alert writes the same `schannel: next InitializeSecurityContext failed: ...` line as Schannel curl 8.22.0, including the `--tls-max 1.2` against a TLS 1.3-only server case.

## Context

Found by BL-3235's CLI TLS differential run (cases `tls12-server-tls13only`, `tls-public-badssl-rc4`, `tls-public-badssl-dh480`, `tls-max-1.2-vs-1.3`). For SEC_E_ILLEGAL_MESSAGE real curl writes "... SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log." where Curl writes "... - The message received was unexpected or badly formatted." (curl's schannel.c replaces the system text for this code). For `--tls-max 1.2` against a TLS 1.3-only server real curl fails with SEC_E_UNSUPPORTED_FUNCTION (0x80090302) "The function requested is not supported", Curl with SEC_E_ILLEGAL_MESSAGE. Find where Curl.Tls.UnitLibrary words Schannel failures (narrow touches once found).

## Acceptance criteria

- [ ] Unit tests pin both measured messages.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
