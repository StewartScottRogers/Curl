---
id: BL-3291
title: Measure and match --cert-status on the Schannel build (tls-cert-status)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3291 — Measure and match --cert-status on the Schannel build (tls-cert-status)

## Goal

`--cert-status` against a server that staples no OCSP response behaves as Schannel curl 8.22.0 does.

## Context

Found by BL-3235's CLI TLS differential run (case `tls-cert-status`, also in the tester's `report_tls.txt`): real curl exits 0 with `ok\n`, Curl exits 91 "No OCSP response received". Measure real curl's --cert-status on Windows (it may be ignored by the Schannel backend, or only checked with revocation on) and match it; pin each platform's answer in its own test, since the OpenSSL build does check the stapled response.

## Acceptance criteria

- [ ] A unit test pins the Schannel build's --cert-status answer for a server with no stapled OCSP response, as measured from real curl, under an OSCondition for Windows.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9b11d705 c46ca32a.
