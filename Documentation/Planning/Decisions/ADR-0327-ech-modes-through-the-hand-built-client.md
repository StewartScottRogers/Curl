# ADR-0327 — `--ech` modes through the hand-built client

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-711.
Builds on ADR-0151 (every `--ech` mode but `false` goes to the hand-built client), ADR-0233
(the ECH offer in `Curl.Tls`) and ADR-0312 (HTTPS records through DoH).

## Context

No curl build on this machine has ECH, so nothing could be measured. The mingw Schannel build
(8.21.0) and Ubuntu's OpenSSL build (8.18.0, WSL) both list no `ECH` feature in `curl -V`
(2026-10-01), as BL-617 and BL-707 found. curl.se's builds have no ECH either. So the behaviour
below comes from curl 8.21.0's source: `src/tool_operate.c`, `lib/setopt.c` (`CURLOPT_ECH`) and
`lib/vtls/openssl.c` (`ossl_init_ech` and the connect step). The tool passes the `--ech` mode, then
`pn:`, then `ecl:` to `CURLOPT_ECH`. The mode sets `tls_ech`, and `ecl:` adds `CURLECH_CLA_CFG` to it.

## Decision

1. **Modes.** `EchModes.Of` reads `TlsClientOptions` as libcurl combines the values:
   - `false` is off, even with a list.
   - `grease` is GREASE, even with a list.
   - `true` is opportunistic and `hard` is mandatory.
   - With no mode, or one libcurl does not know, an `ecl:` list alone makes it opportunistic.
     Without a list it is off.
   - `pn:` alone turns nothing on.

   `TlsClientRouting` sends every mode but off to the hand-built client.
2. **Where the configuration comes from.** `EchOffer.DecideAsync` takes the `ecl:` list
   (base64) first. Without one it uses the `ech` parameter of the host's HTTPS record, found
   through `IEchConfigListLookup`. Only `DohDnsResolver` implements that interface, because curl
   looks up HTTPS records only through DoH. Each TLS handshake asks the lookup again; curl asks
   while it resolves the name, so this differs in when the DoH query is sent, not in what it says.
   A list that is not base64, does not decode, or has no configuration the client can seal for
   counts as no list.
3. **`pn:`** replaces the public name of every configuration, so it goes in the outer
   `server_name` (OpenSSL's `SSL_ech_set1_server_names`). The HPKE `info` still uses each
   configuration's encoded bytes as published.
4. **The ClientHello.** ECH and GREASE go only in the TLS 1.3 hello. `encrypted_client_hello`
   is added after the profile's measured extensions. A range that does not reach TLS 1.3 offers
   neither.
5. **Failures.**
   - `hard` with no usable list, or with a range below TLS 1.3, fails before any byte is sent
     with exit 35. curl calls no `failf` there, so the text is libcurl's `SSL connect error`.
   - `true` with no usable list sends a plain hello.
   - A server that does not accept the offer ends the handshake with `ech_required`. That is
     exit 101 for `true` and for `hard`, as OpenSSL's `SSL_R_ECH_REQUIRED` becomes
     `CURLE_ECH_REQUIRED`. The text is libcurl's `ECH attempted but failed`, because OpenSSL's
     error string for it could not be measured.
6. **Same text on every platform.** The Schannel build has no ECH, so ADR-0151's rule applies:
   the build that honours the option sets the text.

## Consequences

- `--ech` works on every platform: GREASE, `ecl:` and DoH configurations, `pn:`, `hard`'s exit
  35 and a rejection's exit 101.
- Not measured yet: the `-v` `ECH:` information lines and OpenSSL's error text for exit 101.
  BL-1107 measures them once a curl build with ECH can be run.

## Alternatives considered

- **Ask for the HTTPS record while resolving the name, as curl does.** Lost: the record would
  have to travel from `IDnsResolver` through `TcpConnector` to the TLS provider. The query it
  sends is the same either way.
- **Fall back to GREASE when `true` finds no list.** Lost: OpenSSL's code sends a plain hello then.
