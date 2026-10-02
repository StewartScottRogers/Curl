---
id: BL-1173
title: Measure --ech through --doh-url HTTPS records with an ECH curl build and pin the ECH: HTTPS RR lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1107]
touches: [Record-CurlExchange.ps1, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1173 — Measure --ech through --doh-url HTTPS records with an ECH curl build and pin the ECH: HTTPS RR lines

## Goal

With `--doh-url`, `--ech true` and `--ech hard` write the `ECH: ECHConfig from HTTPS RR` and `ECH: imported ECHConfigList of length N` lines (or `ECH: SSL_set1_ech_config_list failed`). These are measured with an ECH curl build, and the DoH HTTPS query bytes that BL-707 pinned are checked against it.

## Context

- ADR-0359 (BL-1107) took these lines from curl 8.21.0's source. `Record-CurlExchange.ps1` has no DoH server that answers an HTTPS (type 65) query with an `ech` parameter, so BL-1107 could not measure them.
- Extend `Record-CurlExchange.ps1` with a DoH mode that answers A, AAAA and HTTPS queries. Then run the ECH build from BL-1107's Notes (Docker, OpenSSL 4.0.0 with curl 8.21.0) against it.
- curl also prints an `HTTPS-RR: ...` line under `--enable-httpsrr`. Record what it says, and decide in an ADR whether Curl writes it.

## Acceptance criteria

- [ ] The measurements, including the DoH request bytes, are in Notes.
- [ ] `Curl.Networking.UnitTests` pin the lines as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-02: Created.
