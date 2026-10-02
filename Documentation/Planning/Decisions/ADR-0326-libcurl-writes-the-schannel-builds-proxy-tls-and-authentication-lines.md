# ADR-0326: --libcurl writes the Schannel build's proxy, TLS and authentication lines

- Status: Accepted
- Date: 2026-10-01
- Decided by Claude under Stewart's delegation (BL-654)

## Context
BL-653 taught `LibcurlSourceCode` the HTTP, output and connection options. The proxy, TLS and
authentication options still wrote nothing, and nothing stopped a newly parsed option being forgotten.
curl 8.21.0 (mingw, Schannel) was measured on 2026-10-01 with `Record-CurlExchange.ps1 -NoServer` and
`--libcurl - -s` (BL-654 Notes).

## Decision
- Write the lines the Schannel build writes, on every platform: `--capath`, `--crlfile`,
  `--tls13-ciphers`, `--curves`, `--sigalgs`, `--cert-status`, `--engine`, `--no-sessionid`,
  `--socks5-gssapi-service` and their proxy forms write nothing, as measured. The generated text does not
  depend on the platform we run on.
- Order as measured: bearer and proxy lines after `NOBODY`; netrc and `LOGIN_OPTIONS` after
  `FAILONERROR`; `HTTPAUTH` after the request body; `UNRESTRICTED_AUTH`, `AWS_SIGV4`, `PROXYHEADER` and
  `HEADEROPT` among the HTTP-only lines; the TLS block (server then proxy, each pair together) after the
  scheme's lines; SOCKS5 and `SERVICE_NAME` after `IPRESOLVE`; delegation and SASL last.
- Bitmasks (`HTTPAUTH`, `SOCKS5_AUTH`, `SSL_OPTIONS`) are written as curl's tool writes them: each name
  in libcurl's table order whose bits are all still unwritten, then the rest as `<n>UL`, joined by ` |`
  and a new line indented to the value. `--oauth2-bearer` adds 64 and `--aws-sigv4` 128, so curl writes
  `CURLAUTH_NONE` and the number; `--anyauth` starts from `CURLAUTH_ANY` (32-bit, as on Windows) and a
  later `--no-` scheme removes its bit.
- `HEADEROPT` is written for an HTTP transfer through a proxy when the URL is `https` or `-p` tunnels.
  `PROXY_SSLVERSION` is written only with a proxy; the other proxy TLS lines are written without one.
- `-E` is split as curl's Windows tool splits it: `pkcs11:` is never split (and sets type `ENG`), `\:`
  and `\` are escapes, a drive colon stays in the name, an empty password is none, `--pass` wins.
- An enumeration test lists every parsed option as written, known to write nothing, or waiting for
  BL-1106, so an option added later fails it until classified.

## Alternatives considered
- Writing the OpenSSL build's lines off Windows (`CAPATH`, `CRLFILE` and others): not measured here,
  and one fixed text keeps the tests platform-neutral; BL-1106 may revisit with a Linux measurement.
- Doing every remaining option in BL-654: too large for one run; the rest is BL-1106.
