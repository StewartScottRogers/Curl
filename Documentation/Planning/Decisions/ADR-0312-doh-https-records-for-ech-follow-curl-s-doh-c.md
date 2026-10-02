# ADR-0312 — DoH HTTPS records for ECH follow curl's `lib/doh.c`

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-707.

## Context

With `--ech true` or `hard` and `--doh-url`, curl 8.21.0 finds a host's ECHConfigList in its
HTTPS record (RFC 9460), asked for through DoH beside the A and AAAA queries (`docs/ECH.md`).
BL-707 adds that query and the record's decoding to `Curl.Networking.UnitLibrary`, ahead of
BL-711, which applies `--ech`.

Neither curl on this machine can be measured doing it: the Schannel build (8.21.0) and Ubuntu's
OpenSSL build (8.18.0, through WSL) both list no `ECH` or `HTTPSRR` feature, and so never send
an HTTPS query. The A query was measured in BL-639 (ADR-0152).

## Decision

- The HTTPS query is the measured A query with QTYPE 65:
  `00 00 01 00 00 01 00 00 00 00 00 00` `07 example 04 test 00` `00 41 00 01` for
  `example.test`. curl's `doh_req_encode` writes every type the same way, so only QTYPE differs.
- The name asked for is the host on port 443 and `_<port>._https.<host>` on any other port, as
  curl's `doh.c` builds it (RFC 9460 section 9.1). IP literals and `localhost` are never asked.
- `DnsAnswerDecoder` keeps the first four HTTPS records' data undecoded
  (`DnsAnswer.HttpsRecordData`), as `doh_store_https` does (`DOH_MAX_HTTPS`), and they count as
  content. `DohDnsResolver.ResolveHttpsRecordAsync` decodes the first, as curl decodes only the
  first, with `ServiceBindingRecordDecoder`.
- `ServiceBindingRecordDecoder` reads the keys curl reads (`alpn`, `no-default-alpn`, `port`,
  `ipv4hint`, `ech`, `ipv6hint`) and skips any other, as `doh_resp_decode_httpsrr` does. It is
  stricter than curl in one way: a record that runs past its end or whose known parameter has the
  wrong shape is refused with a `ServiceBindingFailure` rather than read past its end or half
  stored. A record refused this way gives no ECH configuration, which is what curl does with a
  record it cannot decode. Key order is not checked, as curl does not check it.

## Consequences

BL-711 gets the ECHConfigList from `ServiceBindingRecord.EchConfigList`. When an ECH-capable
curl build is available, BL-711's measurement should confirm the query bytes above; a difference
is a new task against this ADR.

## Alternatives considered

- Blocking until an ECH-capable build is installed: the query's bytes follow from the measured A
  query and curl's one encoder, so waiting adds no information this task needs.
- Decoding every HTTPS record and picking by priority: curl uses the first record only, so a
  different choice would pick a different configuration than curl does.
