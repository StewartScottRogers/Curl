# ADR-0011 — `--ciphers` and `--tls13-ciphers`: Schannel behaviour on Windows, honoured through `CipherSuitesPolicy` on Linux and macOS

- **Status:** Accepted
- **Date:** 2026-09-26

## Context

Upstream (<https://curl.se/docs/manpage.html>, curl 8.23.0, read 2026-09-26):
`--ciphers` "Specify which cipher suites to use in the connection if it negotiates
TLS 1.2 (1.1, 1.0)"; `--tls13-ciphers` does the same for TLS 1.3. A cipher list curl
cannot apply is exit 59 `CURLE_SSL_CIPHER`
(<https://curl.se/libcurl/c/libcurl-errors.html>).

ADR-0009 makes Curl reproduce, on each platform, the curl build that platform usually
runs: the Schannel build of curl 8.21.0 on Windows, the OpenSSL build on Linux and
macOS. It already pins the exit 59 text for `--ciphers NOSUCHCIPHER` on both builds.

The BCL's only control is `SslClientAuthenticationOptions.CipherSuitesPolicy`, a single
list of `TlsCipherSuite` values (IANA names) covering every TLS version. Its constructor
throws `PlatformNotSupportedException` unless the platform "is a Linux system with
OpenSSL 1.1.1 or higher or a macOS" (the .NET 10.0.12 reference documentation). So it
cannot be used on Windows. OpenSSL-style names such as `ECDHE-RSA-AES128-GCM-SHA256`
have no BCL mapping and need a hand-written table, since no package may be added.

Stewart chose option 1 of BL-060 on 2026-09-26: refuse on Windows as the Schannel build
does, and honour on Linux and macOS through `CipherSuitesPolicy`, accepting both OpenSSL
and IANA names.

### Measurements

All measured 2026-09-26 against `openssl s_server` (self-signed `CN=localhost`,
RSA-2048, TLS 1.2 and 1.3 enabled) on loopback, with `-sS -k -o /dev/null`.

**Windows**: curl 8.21.0 (x86_64-w64-mingw32) libcurl/8.21.0 Schannel.

| Options | Exit | stderr |
| --- | --- | --- |
| `--ciphers BOGUS` | 59 | `curl: (59) schannel: Failed setting algorithm cipher list` |
| `--ciphers ECDHE-RSA-AES128-GCM-SHA256 --tls-max 1.2` (OpenSSL name) | 59 | the same line |
| `--ciphers TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 --tls-max 1.2` (IANA name) | 59 | the same line |
| `--tls13-ciphers TLS_AES_128_GCM_SHA256` | 0 | nothing |
| `--tls13-ciphers BOGUS` | 0 | nothing |

**Linux**: curl 8.18.0 (x86_64-pc-linux-gnu) libcurl/8.18.0 OpenSSL/3.5.5 under WSL 2,
the OpenSSL build ADR-0009 measured with.

| Options | Exit | stderr |
| --- | --- | --- |
| `--ciphers BOGUS` | 59 | `curl: (59) failed setting cipher list: BOGUS` |
| `--ciphers ECDHE-RSA-AES128-GCM-SHA256 --tls-max 1.2` (OpenSSL name) | 0 | nothing |
| `--ciphers TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 --tls-max 1.2` (IANA name) | 0 | nothing |
| `--ciphers ECDHE-RSA-AES128-GCM-SHA256:BOGUS --tls-max 1.2` | 0 | nothing |
| `--tls13-ciphers BOGUS` | 59 | `curl: (59) failed setting TLS 1.3 cipher suite: BOGUS` |
| `--tls13-ciphers TLS_AES_128_GCM_SHA256` | 0 | nothing |
| `--tls13-ciphers TLS_AES_128_GCM_SHA256:BOGUS` | 0 | nothing |

So on OpenSSL a list fails only when no entry in it is known; unknown entries beside a
known one are dropped silently. macOS was not measured (no host); it follows the Linux
rule, as ADR-0009 decides.

## Decision

### Windows (Schannel build)

- **`--ciphers`** is refused. Any value, once a TLS connection is about to be made,
  ends the transfer with exit 59 and, under `-S`, the line
  `curl: (59) schannel: Failed setting algorithm cipher list`. No `CipherSuitesPolicy`
  is constructed.
- **`--tls13-ciphers`** is accepted and ignored: any value, well-formed or not, leaves
  the handshake unchanged and prints nothing. This is what the Schannel build does.

### Linux and macOS (OpenSSL build)

Both options are honoured through `CipherSuitesPolicy`.

- **Name syntax.** A value is a list separated by `:`, `,` or spaces, as OpenSSL
  splits it. Each entry is matched, case-sensitively, first against the IANA name
  (the `TlsCipherSuite` member name, e.g. `TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256`) and
  then against a hand-written table of OpenSSL names (e.g.
  `ECDHE-RSA-AES128-GCM-SHA256`). For TLS 1.3 the OpenSSL and IANA names are the same
  (`TLS_AES_128_GCM_SHA256`).
- **Unknown entries** are dropped silently. If no entry of a list is known, the
  transfer ends with exit 59 and, under `-S`:
  - `--ciphers`: `curl: (59) failed setting cipher list: <the --ciphers value>`
  - `--tls13-ciphers`: `curl: (59) failed setting TLS 1.3 cipher suite: <the --tls13-ciphers value>`
- **`--ciphers` applies to TLS 1.2 and below only.** A TLS 1.3 suite named in it is
  dropped like an unknown entry, and a TLS 1.2 suite named in `--tls13-ciphers` is
  dropped likewise.
- **Composing the one policy.** When either option is given, the policy is the union
  of the TLS 1.2-and-below suites (from `--ciphers`, or every TLS 1.2-and-below suite in
  the name table when it is absent) and the TLS 1.3 suites (from `--tls13-ciphers`, or
  OpenSSL's default `TLS_AES_256_GCM_SHA384`, `TLS_CHACHA20_POLY1305_SHA256`,
  `TLS_AES_128_GCM_SHA256` when it is absent). When neither is given, no policy is set.
- **Order** of the list is kept as given; the BCL passes it to OpenSSL in that order.

### Divergences from upstream curl this accepts

1. **OpenSSL cipher-string keywords and operators** (`HIGH`, `DEFAULT`, `ALL`, `aRSA`,
   `!aNULL`, `+`, `-`, `@STRENGTH`, `@SECLEVEL=n`) are not interpreted; each is an
   unknown entry. A value made only of them (e.g. `--ciphers HIGH:!aNULL`) is exit 59
   in Curl where OpenSSL curl accepts it.
2. **The name table is finite.** A suite OpenSSL knows but the table does not list, or
   that `TlsCipherSuite` has no member for, is an unknown entry.
3. **Defaults filled in when only one option is given** are the name table's full
   TLS 1.2 set and OpenSSL's documented default TLS 1.3 set, not the local OpenSSL's
   configured defaults (`openssl.cnf`, system crypto policies), because the BCL takes
   one explicit list for both versions.
4. **macOS** follows the OpenSSL build, although the curl Apple ships is a LibreSSL
   build (ADR-0009 already accepts this).

Windows has no divergence: both options behave as the measured Schannel build.

## Consequences

- BL-066 implements this: a Windows branch that returns exit 59 for `--ciphers`, and a
  name table plus a policy builder that are testable without a network (pure mapping
  from option strings to `TlsCipherSuite` lists and exit codes).
- The name table is data that must be kept correct by hand; tests pin each entry.
- Scripts that pass OpenSSL keyword strings get exit 59 on Linux and macOS. If that
  proves common, a later ADR can add the keywords.
- The 8.18.0 Linux strings are re-checked against an OpenSSL build of 8.21.0 when one
  is available, as ADR-0009 already requires for its strings.

## Alternatives considered

- **Accept and ignore both options everywhere** (BL-060 option 2): a script that
  restricts ciphers for security would silently get the defaults on Linux, and Windows
  would exit 0 where the Schannel build exits 59. Two divergences instead of none on
  Windows.
- **Refuse every value everywhere with exit 59** (option 3): simple, but breaks every
  Linux and macOS script that names a valid suite, which OpenSSL curl honours.
- **IANA names only on Linux and macOS**: saves the table, but OpenSSL names are what
  curl's own documentation and most scripts use.

Decided by Stewart on 2026-09-26 (BL-060); the detail beyond his choice (separators,
unknown-entry handling, the TLS 1.3 message, default composition) follows the measured
OpenSSL build and was decided by Claude under Stewart's delegation.
