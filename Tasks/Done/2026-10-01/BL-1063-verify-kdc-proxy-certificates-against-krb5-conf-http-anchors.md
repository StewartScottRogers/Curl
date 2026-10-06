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
completed: 2026-10-01
---
# BL-1063 — Verify KDC proxy certificates against krb5.conf http_anchors

## Goal

Curl verifies an `https://` KDC proxy's certificate the way MIT does. It reads `[libdefaults]`/realm `http_anchors` from `krb5.conf` when that setting is present. Without it, the system trust store is used. Curl's own `-k`, `--cacert` and `--capath` stop applying to the KDC proxy.

## Context

- Follow-up from BL-882. `KerberosKdcProxyHttpsTransport` (in `Curl.Networking.UnitLibrary`) connects with `ConnectTarget(UseTls: true)` through the run's connector, so the transfer's TLS options also apply to the KDC proxy.
- MIT `src/lib/krb5/os/sendto_kdc.c` (`setup_tls`, `load_anchor`): `http_anchors` values are `FILE:`, `DIR:` or `ENV:`. When the setting is absent, OpenSSL's default verify paths are used. Verification always happens and does not depend on the application.
- Likely shape: `KerberosConfiguration` exposes the anchors, the proxy transport interface gains them (or a per-realm TLS option), and Networking builds a dedicated TLS provider from them.

## Acceptance criteria

- [x] `Curl.Kerberos.UnitTests` pin `http_anchors` parsing (`FILE:`, `DIR:`, `ENV:`, absent).
- [x] `Curl.Networking.UnitTests` show a KDC proxy whose certificate is not trusted by the anchors failing with an `IOException`, even when the transfer runs with `-k`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for every touched library.

## Notes

- Decided in ADR-0300 (Claude under Stewart's delegation). MIT reads `http_anchors` from the realm only (`k5tls`, `profile_get_values` with `realms`, realm, `http_anchors`), so `KerberosConfiguration.HttpAnchors(realm)` ignores a `[libdefaults]` value, despite the Goal's "[libdefaults]/realm" wording.
- `KerberosHttpAnchor.Parse` reads `FILE:`, `DIR:` and `ENV:` case-sensitively (`strncmp`). `KerberosKdcSender` posts through a new anchored overload of `IKerberosKdcProxyTransport`. Its default implementation runs the old exchange when there are no anchors and refuses any anchor with an `IOException`. A default kept `Curl.Console.UnitTests`' `RecordingProxy` compiling unchanged. That project is in BL-1060's `touches`, so it stayed out of this task's.
- `KerberosKdcProxyHttpsTransport` now connects with `UseTls: false` and runs its own `KerberosKdcProxyTlsClient` (SslStream, CustomRootTrust of the anchors, no revocation check, or the system store). Its public constructor is unchanged, so `CurlComposition` needed no edit. `KerberosHttpAnchorLoader` loads anchors as MIT's `load_anchor` does. A `DIR:` needs at least one file that loads, and dot files are skipped. `ENV:` naming another `ENV:` is refused so it cannot loop.
- SslStream sends its alert synchronously when it refuses a certificate, and `ConnectionStream` refuses synchronous writes (`NotSupportedException`). The client records the validation callback's verdict and names it in the `IOException`.
- Measured: `Measure-CodeQuality.ps1` shows Curl.Kerberos.UnitLibrary 100/100 (626 members) and Curl.Networking.UnitLibrary 100/100 (945 members), worst CRAP 10.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. KDC proxies are verified against the realm's http_anchors (FILE:/DIR:/ENV:) or the system store with their own TLS; -k, --cacert and --capath no longer apply
