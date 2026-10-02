---
id: BL-1171
title: Write curl's ECH: retry_configs lines when a server sends retry_configs for --ech
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1107]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1171 — Write curl's ECH: retry_configs lines when a server sends retry_configs for --ech

## Goal

When a server sends `retry_configs`, `-v` writes curl 8.21.0's `ECH: retry_configs <base64>` and `ECH: retry_configs for <inner> from <outer>, <reason> <rv>` lines. This covers a rejected ECH offer before exit 101, and GREASE answered by an ECH server.

## Context

- ADR-0359 (BL-1107). Today `HandBuiltTlsProvider.ReportEchRejection` always writes `ECH: no retry_configs (rv = 1)`, which is right only for a server that sends none.
- Measured with GREASE against `openssl s_server -ech_key`:
  - `ECH: result: status is sent GREASE, got retry-configs, inner is NULL, outer is NULL`
  - `ECH: retry_configs <the server's list in base64>`
  - `ECH: retry_configs for NULL from NULL, 0 3`
- `Tls13ClientHandshake` checks and drops the retry configs sent in answer to GREASE. A rejection's configs are on `EncryptedClientHelloRetryConfigs`, but they do not reach `TlsHandshakeFailure`.
- Measure the rejected case with a server whose ECH key differs from the `ecl:` list. Use the Docker recipe in BL-1107's Notes.

## Acceptance criteria

- [ ] The rejected case's lines and exit 101 are measured and recorded in Notes.
- [ ] `Curl.Networking.UnitTests` pin both cases' lines.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and the touched libraries keep 100% line and branch coverage.

## Notes

## Log

- 2026-10-02: Created.
