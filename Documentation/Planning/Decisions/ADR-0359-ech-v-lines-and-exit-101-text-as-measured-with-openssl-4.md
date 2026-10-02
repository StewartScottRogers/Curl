# ADR-0359 — `--ech` `-v` lines and exit 101 text, as measured with OpenSSL 4

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1107.
Amends ADR-0327.

## Context

ADR-0327 took `--ech`'s behaviour from source because no curl build on the machine had ECH. For
BL-1107 a curl with ECH was built in a Docker container (`alpine:latest`): OpenSSL 4.0.0
(`./Configure no-docs no-tests`) and curl 8.21.0 (`--with-openssl --enable-ech --enable-httpsrr`).
`curl -V` lists `ECH HTTPSRR`. It ran against `openssl s_server` without ECH, and with
`-ech_key`, inside the same container. The full transcripts are in BL-1107's Notes.

The measurements disagree with ADR-0327 in three places:

- `pn:` alone, or `ecl:` with no mode or with `false`, is `hard`, not off or opportunistic.
  libcurl's `setopt_ech` sets `CURLECH_HARD` when `tls_ech` is still 0. `--ech pn:x` with no list
  fails with exit 35.
- A usable list on a range below TLS 1.3 (`--tls-max 1.2`) fails with exit 35 and OpenSSL's
  `TLS connect error: error:0A0000BF:SSL routines::no protocols available`. This holds under `true`
  as well as `hard`, because curl forces a TLS 1.3 minimum once it is trying ECH.
- Exit 101's text is `ECH required: error:0A0001A8:SSL routines::ech required`.

## Decision

1. **Modes.** `EchModes.Of`: `grease`, `true` and `hard` keep their meaning. Any other mode, or
   none, with a `pn:` or an `ecl:` is `Mandatory`. Without either it is `Off`. An unknown mode is
   exit 43 in curl (`setopt 0x2855 got bad argument`). That is the parser's job, so it is filed
   separately.
2. **Setup lines.** `EchOffer` returns curl's `ossl_init_ech` lines, and `HandBuiltTlsProvider`
   writes them as `-v` info lines before the ClientHello:
   - `grease`: `ECH: will GREASE ClientHello`, even below TLS 1.3.
   - A usable `ecl:` list: `ECH: ECHConfig from command line`.
   - An unusable list: `ECH: SSL_ECH_set1_ech_config_list failed`, then under `true` the
     command-line line again. Under `hard` the transfer stops with exit 35.
   - No list: `ECH: requested but no ECHConfig available`.
   - With `pn:` and a usable list: `ECH: inner: '<host>', outer: '<pn>'`.
   - A list from the host's HTTPS record (DoH): `ECH: ECHConfig from HTTPS RR`, then
     `ECH: imported ECHConfigList of length N` or `ECH: SSL_set1_ech_config_list failed`.
     These lines come from source. The DoH path was not measured, because the recorder has no
     DoH server that serves HTTPS records yet.
3. **Rejection.** When a server that does not take the offer sends no `retry_configs`, the
   provider writes `ECH: no retry_configs (rv = 1)` and fails with exit 101 and the text above,
   on every platform (ADR-0327 point 6).
4. **Not written.**
   - Every TLS handshake in an ECH build logs `ECH: result: status is not attempted`, including
     `--ech false`. Every HTTPS-RR build logs `HTTPS-RR: -`. Neither the Schannel build nor the
     distribution OpenSSL builds have ECH, so their output stays the reference and Curl writes
     neither line.
   - The post-handshake `ECH: result: ...` line for GREASE, `true` without a list, and an accepted
     offer comes after `SSL connection using`. That place is decided by `TlsHandshakeEvent` in
     `Curl.Protocol.Abstractions` and `Curl.Output`, outside this task, so it is filed as a
     follow-up. So are the `ECH: retry_configs` lines for a server that sends some.

## Consequences

- `-v` for `--ech` now shows curl's setup lines, and exit 101 prints the OpenSSL build's text.
- `--ech pn:x` alone, and `--ech false --ech ecl:...`, now offer ECH as `hard`.
- `--ech true --tls-max 1.2` with a usable list now fails, as curl does, and no longer sends a
  plain TLS 1.2 hello.

## Alternatives considered

- **Keep libcurl's generic `ECH attempted but failed`.** Lost: a build that honours `--ech`
  prints OpenSSL's string, and the measurement shows what that string is.
- **Write `ECH: result: status is not attempted` for `--ech false`.** Lost: the platform builds
  Curl matches print nothing there.
