---
id: BL-3289
title: Trust a leaf certificate named by --cacert on the Schannel build (tls-wrong-ca)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3289 — Trust a leaf certificate named by --cacert on the Schannel build (tls-wrong-ca)

## Goal

`curl -sS --ssl-no-revoke --cacert server.pem https://host/` (the CA file holds only the server's own leaf certificate) succeeds as Schannel curl 8.22.0 does, instead of exiting 60 "schannel: the certificate chain is incomplete".

## Context

Found by BL-3235's CLI TLS differential run (case `tls-wrong-ca` in `Curl.Conformance.Differential.UnitLibrary/Cases/CliTls/CliTlsCases.json`, also in the tester's `report_tls.txt`): real exit 0 with `ok\n`, Curl exit 60. Schannel curl builds a custom-root chain store from the --cacert file and accepts a leaf listed in it as a trust anchor. Measure with Record-CurlExchange.ps1 against a self-signed or CA-issued leaf, then fix the --cacert chain building in Curl.Tls.UnitLibrary (narrow touches to the file once found).

## Acceptance criteria

- [ ] A unit test in Curl.Tls.UnitTests pins that a --cacert file holding only the server's leaf certificate verifies that server, as measured from real curl.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
