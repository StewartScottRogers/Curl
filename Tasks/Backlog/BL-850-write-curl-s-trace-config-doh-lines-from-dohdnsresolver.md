---
id: BL-850
title: Write curl's --trace-config doh lines from DohDnsResolver
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-641]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-850 — Write curl's --trace-config doh lines from DohDnsResolver

## Goal

`DohDnsResolver` reports, through an injected sink the composition root connects only under `--trace-config doh`, the lines curl 8.21.0 writes there: `DoH: <DnsMessageFailureText> type A|AAAA for <host>` for an answer that does not decode, `DoH request <curl_easy_strerror text>` for a query whose connection or exchange fails, and the `[DoH] TTL: N seconds` / `[DoH] A: <address>` / `[DoH] AAAA: <address>` lines of a decoded answer.

## Context

- BL-641 built `DohDnsResolver` without these lines; ADR-0152 (and its BL-641 amendment) records the measured texts: `Too small` for a `500` with an empty body, `Out of range`, `No content`, `Bad RCODE`, `Unexpected TYPE`, `DoH request Failure when receiving data from the peer` for a close-delimited body, `DoH request SSL peer certificate or SSH remote key was not OK` for a refused certificate, `[DoH] TTL: 2147483647 seconds` when neither answered.
- The exit-code-to-text table lives in `Curl.Console/CurlEasyErrorText.cs`, which Networking cannot reference: pass the text in (e.g. from `ConnectResult.ErrorMessage` or a mapping injected by the console), or measure which texts can occur and keep them here.
- BL-642 composes the resolver in `Curl.Console` and should wire the sink once this lands.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Tls` and `--trace-config doh`, the exact line order and prefixes (`* [DNS]`, `* [DoH]`) copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin each measured line for a `500` with empty body, an NXDOMAIN, a truncated answer, an answer with no record, a close-delimited body, a refused certificate and a decoded A answer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
