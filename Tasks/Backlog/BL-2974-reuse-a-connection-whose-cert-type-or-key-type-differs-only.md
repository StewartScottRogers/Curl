---
id: BL-2974
title: Reuse a connection whose --cert-type or --key-type differs only in case, as curl 8.22.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/OptionGroupConnectionSettings.cs, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2974 — Reuse a connection whose --cert-type or --key-type differs only in case, as curl 8.22.0 does

## Goal

Two `--next` option groups whose `--cert-type`/`--key-type` (and the `--proxy-` forms) differ only in letter case share a pooled connection, as curl 8.22.0's `Curl_ssl_conn_config_match` compares those types case-insensitively.

## Context

Found by BL-2921. Upstream `tests/unit/unit3304.c` (curl-8_22_0) says the peer key's case-insensitive types are "consistent with the conn-reuse comparison"; confirm in `lib/vtls/vtls.c` (`match_ssl_primary_config`) before changing anything, and measure with real curl if the source is unclear. Curl decides reuse by record equality of `OptionGroupConnectionSettings` (`Curl.Engine.UnitLibrary/OptionGroupConnectionSettings.cs`), whose `TlsClientOptions` compares `CertificateType` and `PrivateKeyType` ordinally, so `PEM` and `pem` open a second connection today. `TlsSessionCache.PeerKey` already upper-cases them (BL-2921). Normalising the types where the settings are read (upper-case) keeps the record equality intact.

## Acceptance criteria

- [ ] An `OptionGroupConnectionSettingsTests` test shows settings read from `--cert-type PEM` and `--cert-type pem` (and the same for `--key-type`) are equal, and `PEM` and `DER` are not, or the task's Notes record, with the curl source line, that curl compares them case-sensitively and nothing changes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Engine.UnitLibrary` keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
