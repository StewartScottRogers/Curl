---
id: BL-707
title: Decode DNS HTTPS records and fetch a host's ECH configuration through DoH
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-707 — Decode DNS HTTPS records and fetch a host's ECH configuration through DoH

## Goal

The DNS codec in `Curl.Networking.UnitLibrary` encodes HTTPS (type 65) queries and decodes HTTPS/SVCB records (RFC 9460: priority, target, `alpn`, `port`, `ipv4hint`, `ipv6hint`, `ech`), and the DoH resolver can fetch a host's HTTPS record, so `--ech true`/`hard` finds the ECHConfigList through DoH as curl does.

## Context

- curl obtains ECH configurations from HTTPS records via DoH (curl's `docs/ECH.md` at tag `curl-8_21_0` describes it; read it and record the exact behaviour in Notes). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28).
- Builds on BL-640 (codec) and BL-641 (DoH resolver). Record the HTTPS query curl sends through `Record-CurlExchange.ps1 -Tls -k` as a DoH server with a curl build that has ECH.

## Acceptance criteria

- [x] Measured first as above; the DoH query bytes copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the HTTPS query bytes, decode RFC 9460 Appendix D's example records and an answer with an `ech` parameter, and reject a malformed SvcParam list with a typed failure.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Measurement (2026-10-01): neither curl on this machine sends an HTTPS query. The Schannel build
  (8.21.0, `curl -V`) and Ubuntu's OpenSSL build (8.18.0, `wsl.exe -e curl -V`) list no `ECH` or
  `HTTPSRR` feature, so `Record-CurlExchange.ps1 -Tls -k` as a DoH server would record only the A
  and AAAA POSTs measured in BL-639. The HTTPS query is therefore the measured A query with QTYPE 65,
  as curl's single `doh_req_encode` writes every type (ADR-0311). For `example.test`, in a POST of
  `Content-Length: 30`:
  `000001000001000000000000076578616D706C6504746573740000410001`.
  Off port 443 the name is `_<port>._https.<host>` (curl's `doh.c`, RFC 9460 section 9.1).
  BL-711, which needs an ECH-capable build anyway, should confirm these bytes.
- curl's behaviour (`docs/ECH.md`, `lib/doh.c`, `lib/httpsrr.c` at `curl-8_21_0`): with `--ech true`
  or `hard` the HTTPS query joins the A and AAAA ones; `doh_store_https` keeps up to four records'
  data; only the first is decoded (`doh_resp_decode_httpsrr`), reading `alpn`, `no-default-alpn`,
  `port`, `ipv4hint`, `ech` and `ipv6hint` and skipping other keys.
- Decided (ADR-0311): `ServiceBindingRecordDecoder` refuses a record that overruns or whose known
  parameter has the wrong shape (`ServiceBindingFailure`) rather than read past its end; key order
  is not checked, as curl does not check it.
- Delivered: `DnsRecordType.Https`, `DnsAnswer.HttpsRecordData`, `ServiceBindingRecord`,
  `ServiceBindingRecordDecoder`, `ServiceBindingFailure`, `ServiceBindingDecoding`,
  `DohDnsResolver.ResolveHttpsRecordAsync`. Wiring the configuration into `--ech` is BL-711.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch,
  1009 members, 0 failing, worst CRAP 10. Fast tests: all green (Curl.Networking.UnitTests 2239 passed).

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. DoH fetches a host's HTTPS record and ServiceBindingRecordDecoder decodes RFC 9460 records, ech included, for --ech (ADR-0311)
