---
id: BL-1063
title: Verify KDC proxy certificates against krb5.conf http_anchors
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-882]
touches: [Curl.Kerberos.UnitLibrary, Curl.Kerberos.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-30
completed:
---
# BL-1063 — Verify KDC proxy certificates against krb5.conf http_anchors

## Goal

Curl verifies an `https://` KDC proxy's certificate the way MIT does. It reads `[libdefaults]`/realm `http_anchors` from `krb5.conf` when that setting is present. Without it, the system trust store is used. Curl's own `-k`, `--cacert` and `--capath` stop applying to the KDC proxy.

## Context

- Follow-up from BL-882. `KerberosKdcProxyHttpsTransport` (in `Curl.Networking.UnitLibrary`) connects with `ConnectTarget(UseTls: true)` through the run's connector, so the transfer's TLS options also apply to the KDC proxy.
- MIT `src/lib/krb5/os/sendto_kdc.c` (`setup_tls`, `load_anchor`): `http_anchors` values are `FILE:`, `DIR:` or `ENV:`. When the setting is absent, OpenSSL's default verify paths are used. Verification always happens and does not depend on the application.
- Likely shape: `KerberosConfiguration` exposes the anchors, the proxy transport interface gains them (or a per-realm TLS option), and Networking builds a dedicated TLS provider from them.

## Acceptance criteria

- [ ] `Curl.Kerberos.UnitTests` pin `http_anchors` parsing (`FILE:`, `DIR:`, `ENV:`, absent).
- [ ] `Curl.Networking.UnitTests` show a KDC proxy whose certificate is not trusted by the anchors failing with an `IOException`, even when the transfer runs with `-k`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

## Log

- 2026-09-30: Created.
