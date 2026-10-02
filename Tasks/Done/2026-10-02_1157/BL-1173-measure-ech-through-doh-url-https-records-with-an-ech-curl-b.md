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
completed: 2026-10-02
---
# BL-1173 — Measure --ech through --doh-url HTTPS records with an ECH curl build and pin the ECH: HTTPS RR lines

## Goal

With `--doh-url`, `--ech true` and `--ech hard` write the `ECH: ECHConfig from HTTPS RR` and `ECH: imported ECHConfigList of length N` lines (or `ECH: SSL_set1_ech_config_list failed`). These are measured with an ECH curl build, and the DoH HTTPS query bytes that BL-707 pinned are checked against it.

## Context

- ADR-0359 (BL-1107) took these lines from curl 8.21.0's source. `Record-CurlExchange.ps1` has no DoH server that answers an HTTPS (type 65) query with an `ech` parameter, so BL-1107 could not measure them.
- Extend `Record-CurlExchange.ps1` with a DoH mode that answers A, AAAA and HTTPS queries. Then run the ECH build from BL-1107's Notes (Docker, OpenSSL 4.0.0 with curl 8.21.0) against it.
- curl also prints an `HTTPS-RR: ...` line under `--enable-httpsrr`. Record what it says, and decide in an ADR whether Curl writes it.

## Acceptance criteria

- [x] The measurements, including the DoH request bytes, are in Notes.
- [x] `Curl.Networking.UnitTests` pin the lines as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Curl.Networking.UnitLibrary` keeps 100% line and branch coverage.

## Notes

### The recorder's DoH mode

`Record-CurlExchange.ps1` gained `-DohPort` and `-DnsEchConfigList`. `-DohPort` serves DoH (POST body or
GET `dns=`) through the existing DNS responder and logs each query in `dns.txt` with the transport `doh`.
It speaks HTTPS with the `-Tls` throwaway certificate: a release curl refuses an `http://` DoH URL
(`Protocol "http" is disabled`, `DoH request Unsupported protocol`; only a debug build allows it).
`-DnsEchConfigList` answers a type 65 query with `1 . ech=<list>` (RFC 9460 key 5).

### Measurements (2026-10-02)

The `curl-ech:8.21.0` image from BL-1107 (curl 8.21.0, OpenSSL 4.0.0, ECH and HTTPSRR). Inside it,
`openssl s_server -www` with `-ech_key` from `openssl ech -public_name example.com` on 9443 and 443, and
without ECH on 8443. The recorder ran on Windows with `-NoServer -DohPort 8053 -ListenAddress 0.0.0.0 -Curl docker`.
curl's arguments were `exec ech-box curl -sS -v -k --doh-insecure --doh-url https://192.168.65.254:8053/dns-query <args>`.
The DoH answer for A was 127.0.0.1. The list was
`AD7+DQA6kwAgACC51/Ma8uuiPkyir5tuURRmE6scOVYKdlidZZStbLY0ZgAEAAEAAQALZXhhbXBsZS5jb20AAA==` (64 bytes), and the
"bad" list was `00 04 fe 0d 00 00` (6 bytes).

| Args | HTTPS record | Lines (`ECH:` and `HTTPS-RR`) | Exit |
| --- | --- | --- | --- |
| `--ech true https://ech.example:9443/` | list | `Some HTTPS RR to process`, `HTTPS-RR: 1 . ech=<64 bytes>`, `ECH: ECHConfig from HTTPS RR`, `ECH: imported ECHConfigList of length 64`, `ECH: result: status is bad name (tolerated without peer verification), inner is ech.example, outer is example.com` | 0 |
| `--ech hard` (9443) | list | same as `true` | 0 |
| `--ech true` (9443) | bad | `HTTPS-RR: 1 . ech=<6 bytes>`, `ECH: ECHConfig from HTTPS RR`, `ECH: SSL_set1_ech_config_list failed`, `ECH: result: status is not configured, inner is NULL, outer is NULL` | 0 |
| `--ech hard` (9443) | bad | `ECH: ECHConfig from HTTPS RR`, `ECH: SSL_set1_ech_config_list failed`, `curl: (35) SSL connect error` | 35 |
| `--ech true` (9443) | none | `HTTPS-RR: -`, `ECH: requested but no ECHConfig available`, `ECH: result: status is not configured, ...` | 0 |
| `--ech hard` (9443) | none | `HTTPS-RR: -`, `ECH: requested but no ECHConfig available`, `curl: (35) SSL connect error` | 35 |
| no `--ech` (8443) | list | `HTTPS-RR: 1 . ech=<64 bytes>`, `ECH: result: status is not attempted`; HTTPS query still sent | 0 |
| `--ech grease` (8443) | list | `ECH: will GREASE ClientHello`, `ECH: result: status is sent GREASE, ...`; the list is ignored | 0 |
| `--ech true https://ech.example/` (443) | list | as on 9443 | 0 |

The DoH queries (DNS message bodies), lowercase hex. Their order varies between runs because the three are concurrent:
- A: `00000100000100000000000003656368076578616d706c650000010001`
- AAAA: `00000100000100000000000003656368076578616d706c6500001c0001`
- HTTPS, port 9443: `000001000001000000000000055f39343433065f687474707303656368076578616d706c650000410001` (`_9443._https.ech.example`)
- HTTPS, port 443: `00000100000100000000000003656368076578616d706c650000410001` (the bare host)

These confirm BL-707's query bytes (ID 0, flags 0100, the type 65 query name) and every `ECH:` line ADR-0359 took
from source, so no production behaviour changed. `EchOffer`'s doc comments now say the lines are measured.

### Decisions and pins

- ADR-0369: the `Some HTTPS RR to process` and `HTTPS-RR:` lines, and the HTTPS query sent on every resolve, come from
  `--enable-httpsrr`. The platform builds lack that feature, so Curl writes neither line and asks only under `--ech true`/`hard`.
- `HandBuiltTlsProviderTests.MeasuredEchLines` gained six rows that pin the measured list, the bad list, `hard` with no record, and `grease`.
- `DohDnsResolverTests.ResolveHttpsRecordAsync_WritesTheMeasuredQueryBytes` pins the two measured HTTPS query messages.
- Coverage: only doc comments changed in `Curl.Networking.UnitLibrary`, so its 100/100 from BL-1107 stands.
- `Documentation/Planning/Decisions` (ADR-0369 and its README row) is outside `touches`; rule 3 allows a new ADR.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --ech through --doh-url measured with an ECH curl build; ECH: HTTPS RR lines and DoH query bytes pinned; recorder serves DoH
