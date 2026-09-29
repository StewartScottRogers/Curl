# ADR-0151 — The ten TLS options are accepted everywhere and run on the hand-built client with the honouring build's text

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-617.

## Context

The conformance audit of 2026-09-28 (row 18, Major) found `--curves`, `--sigalgs`,
`--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`,
`--tlsuser`, `--tlspassword` and `--tlsauthtype` known to Curl's alias table but not
parsed, so today each is refused as "the installed libcurl version does not support
this" (ADR-0137). Stewart's standing rule (2026-09-28) is that what any official curl
build does, Curl does on every platform, with the platform's curl's text wherever both
do the same thing; an ADR decides how, never whether. ADR-0009 and ADR-0011 govern the
text; ADR-0140 decides the hand-built TLS client in `Curl.Tls.UnitLibrary` and the
routing rule that sends a transfer to it only when `SslStream` cannot do what the
command line asks, with one row per option. This ADR measures each option on each
build and fixes its row and its text.

### Measurements

Measured 2026-09-28 with `Record-CurlExchange.ps1 -Tls`, every run `-sS -k` plus the
option, against `https://localhost:<port>/` served by the recorder's self-signed RSA
certificate over `SslStream` (Windows 11), whose canned answer is an empty `200`. The
builds:

- **Schannel**: mingw curl 8.21.0, Schannel (the Windows reference, ADR-0018).
- **curl.se**: curl.se's official Windows build, curl 8.18.0 with LibreSSL 4.2.1
  (WinGet `cURL.cURL`; features include `CAcert`, `HTTP3`, no `TLS-SRP`).
- **OpenSSL**: Ubuntu curl 8.18.0 with OpenSSL 3.5.5 under WSL (`-ListenAddress
  172.26.96.1 -Curl wsl.exe`, `--resolve` to the host), features include `TLS-SRP`.

An empty stderr cell means nothing was printed; stdout was empty in every run unless
stated.

| Option (value) | Schannel | curl.se (LibreSSL) | OpenSSL |
| --- | --- | --- | --- |
| `--curves X25519` | exit 0 | exit 0 | exit 0 |
| `--curves bogus` | exit 0 (ignored) | exit 59 `curl: (59) failed setting curves list: 'bogus'` | exit 59, the same line |
| `--sigalgs ECDSA+SHA256` | exit 0 (ignored) | exit 0 (ignored) | exit 35 `curl: (35) TLS connect error: error:0A000126:SSL routines::unexpected eof while reading` (the RSA-only server cannot sign with ECDSA and closes) |
| `--sigalgs rsa_pss_rsae_sha256` | exit 0 | exit 0 | exit 0 |
| `--sigalgs bogus` | exit 0 (ignored) | exit 0 (ignored) | exit 59 `curl: (59) failed setting signature algorithms: 'bogus'` |
| `--tls-earlydata` (first connection, so no session to resume) | exit 0 | exit 0 | exit 0 |
| `--ech true`, `hard`, `grease` | exit 2 `curl: option --ech: the installed libcurl version does not support this` then `curl: try 'curl --help' or 'curl --manual' for more information` | exit 2, the same two lines | exit 2, the same two lines |
| `--ssl-sessions sess.bin` | exit 2 `curl: option --ssl-sessions: the installed libcurl version does not support this` and the try line | exit 2, the same | exit 2, the same |
| `--tlsuser u --tlspassword p` | exit 2 `curl: option --tlsuser: the installed libcurl version does not support this` and the try line | exit 2, the same | exit 35 `curl: (35) TLS connect error: error:0A000126:SSL routines::unexpected eof while reading` (curl offers only the SRP suites; the server shares none and closes) |
| `--tlsauthtype SRP --tlsuser u --tlspassword p` | exit 2 `curl: option --tlsauthtype: the installed libcurl version does not support this` and the try line | exit 2, the same | exit 35, the same line as above |
| `--tlsauthtype bogus …` | exit 2, as `SRP` | exit 2, as `SRP` | exit 2 `curl: option --tlsauthtype: the installed libcurl version does not support this` and the try line |
| `--no-sessionid` | exit 0 | exit 0 | exit 0 |
| `--ssl-allow-beast` | exit 0 | exit 0 | exit 0 |
| `--tlsv1.0 --tls-max 1.0` | exit 35 `curl: (35) schannel: failed to receive handshake, SSL/TLS connection failed` | exit 35 `curl: (35) TLS connect error: error:1404E0BF:SSL routines:ST_BEFORE_CONNECT:no protocols available` | exit 35 `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available` |
| `--tlsv1.1 --tls-max 1.1` | exit 35, as 1.0 | exit 35, as 1.0 | exit 35, as 1.0 |
| `--tlsv1.0 --tls-max 1.0 --ciphers DEFAULT@SECLEVEL=0` (and 1.1) | not run | not run | exit 35 `curl: (35) TLS connect error: error:0A000126:SSL routines::unexpected eof while reading`: OpenSSL offered the legacy version and the Windows server refused it |
| `--engine bogus` | exit 0 (ignored) | exit 53 `curl: (53) OpenSSL engine not found` | exit 53 `curl: (53) Failed to initialize provider: error:12800067:DSO support routines::could not load the shared library` |
| `--engine dynamic` | not run | not run | exit 66 `curl: (66) Failed to initialise SSL Engine 'dynamic': error:00000000:lib(0)::reason(0)` |

Without a server (`curl -sS <option>`):

| Command | Schannel | curl.se (LibreSSL) | OpenSSL |
| --- | --- | --- | --- |
| `--engine list` | exit 0, stdout `Build-time engines:\n  <none>\n` | exit 0, the same stdout | exit 0, stdout `Build-time engines:\n  dynamic\n` |
| `--dump-ca-embed` | exit 0, stdout empty | exit 0, stdout Mozilla's CA bundle (`## Bundle of CA Root Certificates` … `Certificate data from Mozilla as of: Wed Oct 29 14:28:33 2025 GMT`) | exit 0, stdout empty |

What curl's source adds for the two options no measured build has (tag `curl-8_21_0`):

- `--ech` needs a libcurl built with ECH (`docs/cmdline-opts/ech.md`: modes `false`,
  `grease`, `true`, `hard`, `ecl:<b64>`, `pn:<name>`; `hard` needs TLS 1.3 and a
  configuration from DoH or `ecl:`). A refused ECH fails with exit 101,
  `CURLE_ECH_REQUIRED`, whose `curl_easy_strerror` text is "ECH attempted but failed";
  the OpenSSL backend prints `ECH required: <OpenSSL error>` (`lib/vtls/openssl.c`).
- `--ssl-sessions` needs a libcurl built with the experimental SSLS-EXPORT feature
  (`src/tool_getparam.c` returns `PARAM_LIBCURL_DOESNT_SUPPORT` otherwise). The file
  holds one base64 ticket per line with salted, hashed host names
  (`docs/cmdline-opts/ssl-sessions.md`, `lib/vtls/vtls_spack.c`).
- `--tlsauthtype` accepts exactly `SRP` (a case-sensitive `strcmp`) and otherwise
  returns `PARAM_LIBCURL_DOESNT_SUPPORT`, which is the measured OpenSSL `bogus` line.
  With SRP credentials and no `--ciphers`, the OpenSSL backend sets the cipher list to
  `SRP`.
- `--dump-ca-embed` "writes the CA bundle embedded in curl to standard output, then
  quit[s]. If curl was not built with a default CA bundle embedded, the output is
  empty" (`docs/cmdline-opts/dump-ca-embed.md`).

## Decision

Every option is accepted on every platform. The general rules:

1. **Route.** Each option that `SslStream` cannot carry is a row in ADR-0140's routing
   function (BL-708): when the option is in force, the transfer's TLS runs on the
   hand-built client on Windows, Linux and macOS alike. The rows ADR-0140 lists stand
   unchanged; this ADR adds none and removes none.
2. **Text.** Where the platform's curl honours an option, Curl prints what it prints.
   Where the platform's curl ignores or refuses an option that another official build
   honours, Curl prints what the honouring build prints (ADR-0140, "Failures and
   text"): curl.se's LibreSSL build on Windows where it honours the option, otherwise
   the OpenSSL build. So on Windows a value the Schannel build silently ignores, such as
   `--curves bogus`, fails as the build that applies it does. That is the one visible
   difference from the Schannel build, and it is the price of doing what the option asks.
3. **Parse checks** are curl 8.21.0's (`src/tool_getparam.c`), the same on every
   platform; a refused value prints curl's line for it.

| Option | Schannel build | OpenSSL build | curl.se Windows build | Curl's route (Windows; Linux and macOS) | Text Curl prints |
| --- | --- | --- | --- | --- | --- |
| `--curves <list>` | accepts, ignores | applies | applies | hand-built; hand-built | An unknown name: exit 59 `curl: (59) failed setting curves list: '<list>'` on every platform. No shared group: exit 35 with the honouring build's handshake text (curl.se's on Windows, OpenSSL's elsewhere), pinned by BL-709. |
| `--sigalgs <list>` | accepts, ignores | applies | accepts, ignores | hand-built; hand-built | An unknown name: exit 59 `curl: (59) failed setting signature algorithms: '<list>'` on every platform (only the OpenSSL build applies it, so its text is used on Windows too). A server that cannot sign with any: exit 35 with OpenSSL's text, pinned by BL-709. |
| `--tls-earlydata` | accepts, never sends 0-RTT | applies on a resumed TLS 1.3 session | accepts | hand-built; hand-built | Nothing on success; `%{tls_earlydata}` reports the bytes sent early (BL-710). |
| `--ech <mode>` | refuses (exit 2) | refuses (exit 2) | refuses (exit 2) | `false`: `SslStream` as today; any other mode: hand-built; the same | Parse errors as curl's `parse_ech`. `hard` without ECH, or ECH rejected: exit 101 with the ECH-enabled OpenSSL backend's `ECH required: …` line, pinned by BL-711. No build measured here has ECH; curl's source does (ECH-enabled libcurl), so Curl does. |
| `--ssl-sessions <file>` | refuses (exit 2) | refuses (exit 2) | refuses (exit 2) | hand-built; hand-built | curl's file format (`vtls_spack.c`), so a file one tool writes the other reads (ADR-0140's `TlsSessionCodec`); an unwritable file fails as curl's SSLS-EXPORT build does, pinned by BL-710. |
| `--engine list` | prints `<none>` | prints `dynamic` | prints `<none>` | no TLS; no TLS | `Build-time engines:\n  <none>\n`, exit 0, on every platform. Curl has no crypto engines: listing OpenSSL's `dynamic` loader, which Curl cannot run, would say something untrue. |
| `--engine <name>` | accepts, ignores | loads the engine | loads the engine | `SslStream` (ignored, as the Schannel build); none: fails before connecting | Windows: accepted and ignored, exit as the transfer. Linux and macOS: `dynamic` fails exit 66 `curl: (66) Failed to initialise SSL Engine 'dynamic': error:00000000:lib(0)::reason(0)`, any other name exit 53 `curl: (53) Failed to initialize provider: error:12800067:DSO support routines::could not load the shared library`, as the OpenSSL build answers for an engine it cannot load. An engine is an OpenSSL shared-library plug-in; a process without OpenSSL has none to load, so this is the honest answer of every build without the named engine, not a refusal of the option. |
| `--dump-ca-embed` | prints nothing | prints nothing | prints Mozilla's bundle | no TLS; no TLS | Nothing, exit 0, on every platform. Curl embeds no CA bundle: it trusts the operating system's store, as the Schannel and OpenSSL builds do, and an embedded bundle that trust never used would be a document that lies about the code. So there is nothing to refresh. |
| `--tlsuser`, `--tlspassword` | refuse (exit 2) | apply (SRP) | refuse (exit 2) | hand-built; hand-built | Accepted everywhere. With no `--ciphers`, the SRP suites only, as the OpenSSL backend. A server without SRP: exit 35 with OpenSSL's text (measured above) on every platform; a wrong password: pinned by BL-712. |
| `--tlsauthtype <type>` | refuses (exit 2) | accepts exactly `SRP` | refuses (exit 2) | no route of its own (SRP is the only type) | `SRP`: accepted. Anything else: exit 2 `curl: option --tlsauthtype: the installed libcurl version does not support this` and the try line, as curl's own value check. |
| `--no-sessionid` | applies | applies | applies | hand-built; hand-built | Nothing. The hand-built client neither offers nor stores a session; `SslStream` cannot stop the operating system's process-wide cache. |
| `--ssl-allow-beast` | applies | applies | applies | hand-built when the version range includes TLS 1.0; the same | Nothing. It turns off the TLS 1.0 CBC empty-fragment split of ADR-0150 (which amends ADR-0140's 1/n-1 wording). |
| `--tlsv1.0`/`--tlsv1.1` with `--tls-max 1.0`/`1.1` | fails 35 (Windows 11 disables them) | fails 35 at the default security level; offers them with `@SECLEVEL=0` | fails 35 (LibreSSL 4 has no TLS 1.0 or 1.1) | hand-built; hand-built | Curl offers the legacy versions on every platform, as the Schannel build does on a Windows with them enabled and the OpenSSL build does at security level 0. A server that refuses: exit 35 with the platform's curl's handshake-failure text, pinned by BL-714. |

The proxy forms (`--proxy-tlsuser`, `--proxy-tlspassword`, `--proxy-tlsauthtype`,
`--proxy-ssl-allow-beast`) behave as their origin forms for the HTTPS-proxy handshake,
as ADR-0140 routes it (ADR-0095).

## Consequences

- No cell above refuses an option any official build honours. `--ech` and
  `--ssl-sessions` work in Curl although no build measured here has them, because
  curl's source builds them.
- On Windows, a `--curves` or `--sigalgs` value the Schannel build ignores now takes
  effect, and a bad value fails with exit 59; a script that passed a harmless value
  keeps working.
- ADR-0137's refusal stops applying to these ten options once BL-618 parses them.
- The tasks that implement this decision:
  - **BL-618** parses all ten with curl 8.21.0's value checks, carries them into
    `TlsClientOptions`, prints `--engine list` and `--dump-ca-embed` as above, and
    applies `--engine <name>` as above (ignored on Windows, 53 or 66 elsewhere). None of
    the ten is applied through `SslStream` beyond that.
  - **BL-708** adds the rows of ADR-0140 this table relies on to the routing function.
  - **BL-709** `--curves` and `--sigalgs`; **BL-710** `--tls-earlydata` and
    `--ssl-sessions`; **BL-711** `--ech`; **BL-712** TLS-SRP; **BL-713**
    `--no-sessionid` and `--ssl-allow-beast`; **BL-714** TLS 1.0 and 1.1. Each pins
    the failure text this ADR leaves to it, measured as above.

## Alternatives considered

- **Keep the Schannel build's behaviour on Windows** (ignore `--curves` and
  `--sigalgs`, refuse SRP, ECH and session files). Byte-for-byte closer to the
  Windows reference, but it refuses what other builds do, against the standing rule.
  Rejected.
- **Embed Mozilla's bundle as curl.se's build does.** `--dump-ca-embed` would print
  something, but Curl would either trust a second store behind the operating system's
  back or carry a bundle it never uses, and would need a refresh process. Rejected: the
  platform builds embed none.
- **List `dynamic` for `--engine list` on Linux and macOS** to match the OpenSSL build.
  Rejected: Curl cannot load an OpenSSL engine, so the list would be false.
- **Load OpenSSL engines through native interop.** Would tie Curl to the system's
  libcrypto, against the BCL-only rule and native AOT's trimming. Rejected.
