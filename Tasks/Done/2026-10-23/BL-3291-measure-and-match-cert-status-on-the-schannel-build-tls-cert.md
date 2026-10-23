---
id: BL-3291
title: Measure and match --cert-status on the Schannel build (tls-cert-status)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs, Curl.Engine.UnitTests/TlsClientOptionsMappingTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3291 — Measure and match --cert-status on the Schannel build (tls-cert-status)

## Goal

`--cert-status` against a server that staples no OCSP response behaves as Schannel curl 8.22.0 does.

## Context

Found by BL-3235's CLI TLS differential run (case `tls-cert-status`, also in the tester's `report_tls.txt`): real curl exits 0 with `ok\n`, Curl exits 91 "No OCSP response received". Measure real curl's --cert-status on Windows (it may be ignored by the Schannel backend, or only checked with revocation on) and match it; pin each platform's answer in its own test, since the OpenSSL build does check the stapled response.

## Acceptance criteria

- [x] A unit test pins the Schannel build's --cert-status answer for a server with no stapled OCSP response, as measured from real curl, under an OSCondition for Windows.
- [x] The build is clean and the fast tests pass.

## Notes

- Measured curl 8.22.0 Schannel: `--cert-status` is silently ignored (exit 0 with the body against BL-3235's no-stapling server; `-v` against example.com prints the same lines with and without it). ADR-0795 records it.
- The check is not in Curl.Tls.UnitLibrary: `--cert-status` reaches TLS through `Curl.Engine.UnitLibrary/TlsClientOptionsMapping.cs` (`RequireCertificateStatus`), so that file and its test were added to touches; no task in Doing on origin/work/dark-factory named them. Changing the mapping (not `HandBuiltTlsProvider`, as ADR-0793 did for `--curves`) also keeps the transfer on `SslStream`, and avoids `Curl.Networking.UnitTests`, which BL-3306 holds.
- Default taken: `--doh-cert-status` is left as it is, since it was not measured.
- Platform check: the Windows answer is pinned under `[OSCondition(OperatingSystems.Windows)]` and the OpenSSL answer under the Exclude-Windows condition; the helper's test passes the platform in, so it gives the same answer everywhere. CI on Linux and macOS is covered by keeping the change platform-neutral.
- Fast tests all green on Windows; Measure-CodeQuality for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 9b11d705 c46ca32a.
- 2026-10-23: Doing -> Done. --cert-status is ignored on the Schannel build, as curl 8.22.0 Schannel: no stapled-status check, exit 0
