---
id: BL-1170
title: Write curl's post-handshake ECH: result line for --ech after SSL connection using
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1107]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1170 — Write curl's post-handshake ECH: result line for --ech after SSL connection using

## Goal

After a completed hand-built handshake under `--ech`, `-v` writes curl 8.21.0's `* ECH: result: status is ...` line straight after `SSL connection using ...`, as the OpenSSL 4.0.0 ECH build does.

## Context

- ADR-0359 (BL-1107) measured the lines with curl 8.21.0 on OpenSSL 4.0.0 (Docker build, recipe in BL-1107's Notes). The line goes between `SSL connection using` and `ALPN: server did not agree...`, so it needs a field on `TlsHandshakeEvent` (`Curl.Protocol.Abstractions`) that `Curl.Output`'s `OpenSslHandshakeText` writes. `HandBuiltTlsProvider` fills it in.
- Measured texts:
  - `grease`: `ECH: result: status is sent GREASE, inner is NULL, outer is NULL`. This holds at TLS 1.2 as well.
  - `true` with no usable list: `ECH: result: status is not configured, inner is NULL, outer is NULL`.
  - An accepted offer under `-k`: `ECH: result: status is bad name (tolerated without peer verification), inner is <host>, outer is <public name or pn:>`.
  - An accepted offer with verification: `success, inner is <host>, outer is <name>`. This comes from source in `lib/vtls/openssl.c` and was not measured.
- Write nothing for `--ech false` or with no `--ech`. The platform builds have no ECH (ADR-0359 point 4).

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` and `Curl.Output.UnitTests` pin each status line above in its place after `SSL connection using`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each touched library.

## Notes

## Log

- 2026-10-02: Created.
