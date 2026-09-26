# ADR-0009 — TLS failure messages, `--capath`, `--cert` formats and the trust store match the platform's usual curl build

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

The TLS exit codes are the same in every curl build: 35 `CURLE_SSL_CONNECT_ERROR`,
58 `CURLE_SSL_CERTPROBLEM`, 59 `CURLE_SSL_CIPHER`, 60 `CURLE_PEER_FAILED_VERIFICATION`,
77 `CURLE_SSL_CACERT_BADFILE` (<https://curl.se/libcurl/c/libcurl-errors.html>). BL-062
implements those codes. Everything else about a TLS failure depends on which TLS
library curl was built with: the text after `curl: (NN) `, whether `--capath` is
honoured, which `--cert` formats load, and which trust store is used by default.

Curl publishes native binaries for Windows, Linux and macOS (BL-028). .NET's
`SslStream` sits on Schannel on Windows and on OpenSSL on Linux, so the BCL could
reproduce either family of behaviour on either platform, but only by mapping
exceptions to text. The reference binary in `Documentation/Product/Product-Overview.md`
is a Schannel build. The manpage (<https://curl.se/docs/manpage.html>, curl 8.23.0,
read 2026-09-26) says `--cert` "must be PEM format" and that `--capath` verifies the
peer against a certificate directory; the Schannel build does neither.

Stewart chose on 2026-09-26 (BL-059): match the platform's usual curl.

### Measurements

All measured 2026-09-26 against `openssl s_server` on loopback with a self-signed
`CN=localhost` RSA-2048 certificate, `-sS` given unless stated.

**Windows** — curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel,
Release-Date 2026-06-24. Captured through a pipe, stderr lines end in CRLF.

| Command | Exit | First stderr line |
| --- | --- | --- |
| `curl https://localhost:18443/` | 60 | `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.` |
| `curl --cacert cert.pem https://127.0.0.1:18443/` (name mismatch) | 60 | `curl: (60) schannel: CertFindExtension() returned no extension.` |
| `curl --cacert bad.pem …` (file of garbage), `--cacert empty.pem` (empty file), `--cacert cert.p12` | 60 | `curl: (60) schannel: the certificate or certificate chain is based on an untrusted root` |
| `curl --cacert dir.pem …` (`dir.pem` is a directory) | 77 | `curl: (77) schannel: failed to open CA file 'dir.pem'` |
| `curl --tlsv1.3 -k` against a TLS 1.2-only server | 35 | `curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported` |
| `curl -k --ciphers NOSUCHCIPHER …` | 59 | `curl: (59) schannel: Failed setting algorithm cipher list` |
| `curl -k --cert cert.pem --key key.pem …` (PEM, no `--cert-type`) | 58 | `curl: (58) schannel: Failed to import cert file cert.pem, last error is 0x80092002` |
| `curl -k --cert cert.pem --cert-type PEM --key key.pem …` | 58 | `curl: (58) schannel: certificate format compatibility error for cert.pem` |
| `curl -k --cert cert.p12 --cert-type DER …` | 58 | `curl: (58) schannel: certificate format compatibility error for cert.p12` |
| `curl -k --cert nonexist.pem …` | 58 | `curl: (58) schannel: Failed to get certificate location or file for nonexist.pem` |
| `curl -k --cert cert.p12 …` and with `--cert-type P12` (PKCS#12, empty password) | 0 | none |
| `curl --no-progress-meter --capath . -k …` | 0 | `Warning: ignoring setting the CA path for the proxy, not supported by libcurl ` (trailing space) then `Warning: with Schannel`; the directory is not used |
| `curl -sS --capath . -k …` | 0 | none: `-s` silences both warnings |

A missing `--cacert` file never reaches TLS: `curl: The file 'x' provided to --cacert
does not exist`, `curl: option --cacert: is badly used here`, exit 2.

**Linux** — curl 8.18.0 (x86_64-pc-linux-gnu) libcurl/8.18.0 OpenSSL/3.5.5, Ubuntu
package `8.18.0-1ubuntu2.4`, Release-Date 2026-01-07, run under WSL 2. This is the
OpenSSL build available on the measuring host; the decision names 8.21.0 as the
reference, so these strings are re-checked against an OpenSSL build of 8.21.0 when one
is available (see Consequences). Lines end in LF.

| Command | Exit | First stderr line |
| --- | --- | --- |
| `curl https://localhost:28443/` | 60 | `curl: (60) SSL certificate OpenSSL verify result: self-signed certificate (18)` |
| `curl --cacert cert.pem https://127.0.0.1:28443/` (name mismatch) | 60 | `curl: (60) SSL: certificate subject name 'localhost' does not match target hostname '127.0.0.1'` |
| `curl --cacert bad.pem …` (file of garbage) | 77 | `curl: (77) error adding trust anchors from file: bad.pem` |
| `curl --tlsv1.3 -k` against a TLS 1.2-only server | 35 | `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version` |
| `curl -k --tls-max 1.2 --ciphers ECDHE-RSA-AES256-GCM-SHA384` against an `AES128-SHA`-only server | 35 | `curl: (35) TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure` |
| `curl -k --ciphers NOSUCHCIPHER …` | 59 | `curl: (59) failed setting cipher list: NOSUCHCIPHER` |
| `curl -k --cert cert.pem --key key.pem …` (PEM) | 0 | none |
| `curl -k --cert cert.p12 --cert-type P12 …` | 0 | none |
| `curl -k --cert cert.p12 …` (PKCS#12, no `--cert-type`) | 58 | `curl: (58) could not load PEM client certificate from cert.p12, OpenSSL error error:0480006C:PEM routines::no start line, (no key found, wrong passphrase, or wrong file format?)` |
| `curl -k --cert nonexist.pem …` | 58 | `curl: (58) could not load PEM client certificate from nonexist.pem, OpenSSL error error:80000002:system library::No such file or directory, (no key found, wrong passphrase, or wrong file format?)` |
| `curl --capath capd …` (`capd` holds the certificate and its `openssl rehash` link) | 0 | none: the directory is used |
| `curl --capath emptyd …` (empty directory), `--capath nonexistdir …` | 60 | the self-signed verify line above: a useless or missing directory is silently ignored |
| `curl -v https://…` | — | `*   CAfile: /etc/ssl/certs/ca-certificates.crt` and `*   CApath: /etc/ssl/certs` |

**Every exit 60, on both builds,** is followed by the same five lines:
`More details here: https://curl.se/docs/sslcerts.html`, a blank line, and
`curl failed to verify the legitimacy of the server and therefore could not` /
`establish a secure connection to it. To learn more about this situation and` /
`how to fix it, please visit the webpage mentioned above.`

**macOS** was not measured: no macOS host was available. The curl Apple ships in
`/usr/bin/curl` is, per its public `--version` output, a LibreSSL build, not OpenSSL.

## Decision

Curl reproduces, on each platform, the curl build that platform usually runs:

- **Windows:** the Schannel build of curl 8.21.0, as measured above.
- **Linux and macOS:** the OpenSSL build of curl 8.21.0.

Tests pin the measured strings; where the reference build was not measured for a case,
the rule below produces the text, and BL-064 or BL-065 measures it before pinning it.

### 1. TLS failure message text

The line is `curl: (NN) ` followed by the reference build's text, as `-S` prints it.

| Exit | Windows (Schannel) | Linux and macOS (OpenSSL) |
| --- | --- | --- |
| 35 | `schannel: next InitializeSecurityContext failed: <SEC_E name> (0x<HRESULT, 8 upper-case hex digits>) - <system message for that HRESULT>`; measured: `SEC_E_UNSUPPORTED_FUNCTION (0x80090302) - The function requested is not supported` | `TLS connect error: <OpenSSL error string>`, the string in `error:XXXXXXXX:<library>::<reason>` form that .NET's OpenSSL exception carries; measured: `error:0A00042E:SSL routines::tlsv1 alert protocol version` |
| 58 | Missing file: `schannel: Failed to get certificate location or file for <path>`. PEM without `--cert-type`: `schannel: Failed to import cert file <path>, last error is 0x80092002`. `--cert-type` `PEM` or `DER`: `schannel: certificate format compatibility error for <path>` | Any file that does not load as the given type: `could not load PEM client certificate from <path>, OpenSSL error <OpenSSL error string>, (no key found, wrong passphrase, or wrong file format?)`; measured for PEM only; the text for a P12 or DER file that fails to load is measured by BL-065 before it is pinned |
| 59 | `schannel: Failed setting algorithm cipher list` | `failed setting cipher list: <the --ciphers value>` |
| 60 | Untrusted root: `schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.`; with a `--cacert` that adds no usable anchor: `schannel: the certificate or certificate chain is based on an untrusted root`; name mismatch: `schannel: CertFindExtension() returned no extension.` | Chain failure: `SSL certificate OpenSSL verify result: <X509 verify error string> (<X509 verify error number>)`, measured `self-signed certificate (18)`; name mismatch: `SSL: certificate subject name '<certificate CN>' does not match target hostname '<URL host>'` |
| 77 | A `--cacert` path that cannot be opened (a directory): `schannel: failed to open CA file '<path>'`. A `--cacert` file with no parsable certificate is **not** 77 on Schannel; it is 60 as above | A `--cacert` file with no parsable certificate: `error adding trust anchors from file: <path>` |

Both platforms follow every exit 60 with the five shared lines above. On Windows the
lines end in CRLF, on Linux and macOS in LF, as each build writes them.

### 2. `--capath`

- **Windows:** ignored. The transfer proceeds without the directory. Unless `-s` is
  given, stderr gets exactly two lines:
  `Warning: ignoring setting the CA path for the proxy, not supported by libcurl `
  (note the trailing space) and `Warning: with Schannel`. With `-s`, nothing.
- **Linux and macOS:** honoured. Every certificate in the directory is added to the
  trust anchors used to verify the peer. An empty or missing directory adds nothing,
  prints nothing, and verification then fails or succeeds on the remaining anchors.

### 3. `--cert` and `--key` formats

- **Windows:** PKCS#12 only, with or without `--cert-type P12`. A PEM or DER file, or
  `--cert-type PEM`/`DER`, fails with exit 58 and the text in section 1; a PEM
  certificate fails even when `--key` is given.
- **Linux and macOS:** PEM by default (`--cert` with `--key`, or a PEM file holding
  both); PKCS#12 with `--cert-type P12`; DER with `--cert-type DER`. A file that does
  not parse as the given type fails with exit 58 and the text in section 1.

### 4. Default trust store

The one the reference build uses:

- **Windows:** the Windows certificate store, which is what `SslStream` uses by
  default.
- **Linux:** the OpenSSL default locations, `/etc/ssl/certs/ca-certificates.crt` and
  `/etc/ssl/certs` on the measured host; `SslStream` on Linux reads the same OpenSSL
  defaults, including `SSL_CERT_FILE` and `SSL_CERT_DIR`.
- **macOS:** the OpenSSL build's CA bundle. `SslStream` on macOS verifies against the
  Keychain, not an OpenSSL bundle, so matching this needs an explicit
  `X509ChainPolicy.CustomTrustStore` loaded from the bundle; BL-064 measures the
  reference build's bundle path before choosing it.

Whether `--cacert` or `--capath` replaces the default store or adds to it was not
measured; BL-064 measures it on each reference build and pins what it finds.

## Consequences

- One build of Curl behaves like the curl a user already has on that platform, so a
  script moved between machines sees the same differences it would see with curl.
- The message text needs a mapping layer per platform: Schannel `SEC_E_*` names and
  system messages from `Win32Exception` on Windows, OpenSSL error strings from the
  inner exception on Linux. Neither can be tested on the other platform against a real
  handshake, so the mapping is unit-tested from recorded exceptions.
- **Divergence from the reference Schannel build:** on Linux and macOS, Curl does not
  behave like the Product Overview's Schannel reference binary. It prints OpenSSL
  text, honours `--capath`, loads PEM `--cert` by default, and reports a garbage
  `--cacert` as 77 rather than 60. This is deliberate and follows from this decision.
- **Divergence from the manpage on Windows:** `--cert` does not accept PEM and
  `--capath` does nothing, as in the Schannel build, even though the manpage says
  otherwise and the BCL could do both.
- **Linux strings come from 8.18.0, not 8.21.0.** Only an Ubuntu OpenSSL build of
  curl 8.18.0 was available to measure. BL-064 and BL-065 re-measure against an
  OpenSSL build of 8.21.0 when one is available; a string that differs is replaced by
  the 8.21.0 text, and this ADR is not edited.
- **macOS is not measured, and its usual curl is not OpenSSL.** Apple's `/usr/bin/curl`
  is a LibreSSL build, so "the platform's usual curl" and "the OpenSSL build" differ on
  macOS. This ADR follows Stewart's instruction and uses the OpenSSL build (what
  Homebrew's `curl` is); if matching Apple's LibreSSL build is wanted instead, that is a
  new ADR superseding this section.
- Tests for Windows-only and OpenSSL-only behaviour run on the matching platform or
  against recorded exceptions; a test that asserts Windows text must not run a real
  handshake on Linux.

## Alternatives considered

1. **Match the Schannel build on every platform.** Lost: Linux users would see
   `schannel:` text and a dead `--capath` that no curl on their platform produces, and
   producing `SEC_E_*` text from OpenSSL errors is a lossy reverse mapping.
2. **Honour the manpage everywhere** (PEM `--cert`, working `--capath`, one text
   family). Lost: on Windows it diverges from the Schannel build that curl for Windows
   ships, which a drop-in replacement exists to match.

## Related

- [ADR-0018](ADR-0018-the-mingw-curl-8-21-0-build-is-the-windows-http-reference.md)
  (2026-09-26) makes the same mingw build, `/mingw64/bin/curl`, the Windows reference
  for HTTP behaviour, and records where System32 `curl.exe` 8.21.0 differs. It adds a
  cross-reference only; the decision above is unchanged.
