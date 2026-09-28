# ADR-0138 — The TLS version range runs from the minimum to `--tls-max`, an empty one is refused while parsing, and neither end reaches an HTTPS proxy

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-502.

## Context

BL-501 taught the parser `-1`/`--tlsv1`, `--tlsv1.0`, `--tlsv1.1`, `--tls-max` and
`--proxy-tlsv1`. Until BL-502 `SslStreamTlsProvider` offered only a minimum of TLS 1.2 or 1.3
(`TlsMinimumVersion`), so TLS 1.0 and 1.1 minimums and every ceiling were ignored. Measured on
2026-09-28 with `Record-CurlExchange.ps1 -Tls -k` (a TLS 1.2-only loopback server) against the
reference build, curl 8.21.0 (Schannel, Windows 11), and with the Ubuntu curl 8.18.0 (OpenSSL
3.5.5) under WSL against `https://example.com/`:

| Arguments | Schannel | OpenSSL |
| --- | --- | --- |
| `--tls-max 1.2`, `--tlsv1.0` | exit 0 | exit 0 |
| `--tls-max 1.1`, `--tls-max 1.0`, `--tlsv1.0 --tls-max 1.1`, `--tlsv1.1 --tls-max 1.1`, `--tlsv1.0 --tls-max 1.0` | exit 35, `schannel: failed to receive handshake, SSL/TLS connection failed` | exit 35, `TLS connect error: error:0A0000BF:SSL routines::no protocols available` |
| `--tlsv1.3 --tls-max 1.2` (any minimum, then a lower `--tls-max`, or `--tls-max default`) | exit 2: `curl: --tls-max set lower than minimum accepted version`, `curl: option --tls-max: is badly used here`, try-help | the same |
| `--tls-max 1.2 --tlsv1.3` (a ceiling, then a higher minimum) | exit 2: `curl: Minimum TLS version set higher than max`, `curl: option --tlsv1.3: is badly used here`, try-help | not measured |
| `-x https://<TLS 1.2-only proxy> --proxy-insecure` with `--tlsv1.3`, `--tls-max 1.1`, `--proxy-tlsv1 --tls-max 1.1` | exit 0: neither the target's minimum nor `--tls-max` reaches the proxy | not measured |

`-s` hides the first of the exit-2 lines; `-sS` shows it. `--tls-max default` after a minimum
is refused because curl compares `default` as lower than every version; before a minimum it
sets no ceiling.

On the same machines `SslStream` answers a TLS 1.0/1.1-only client differently from curl:
Windows 11 refuses to start a handshake offering TLS 1.0 alone or TLS 1.1 alone
(`SEC_E_UNSUPPORTED_FUNCTION`), and one offering both gets the server's `protocol_version`
alert (`SEC_E_ILLEGAL_MESSAGE`); on Linux OpenSSL refuses with exactly curl's
`error:0A0000BF:SSL routines::no protocols available`.

## Decision

1. **One enum for both ends.** `TlsMinimumVersion` becomes `TlsVersion` (`SystemDefault`,
   `Tls10` … `Tls13`, in version order), and `TlsClientOptions` gains `MaximumVersion`.
   `SystemDefault` as a minimum leaves the choice to the operating system; as a ceiling it sets
   none.
2. **The offered set is every version in the range** (`TlsVersionRange.ToSslProtocols`). With no
   minimum and a ceiling below TLS 1.3 the range starts at TLS 1.0, as curl's Schannel build
   starts it; with no minimum and no ceiling (or a TLS 1.3 one) it stays
   `SslProtocols.None`, the operating system's choice, as before. `TlsVersionRange` is the one
   place in `Curl.Networking.UnitLibrary` that names the obsolete `SslProtocols.Tls` and
   `Tls11` (`SYSLIB0039` suppressed there only), as `ObsoleteTlsProtocols` is in
   `Curl.Cli.UnitLibrary`.
3. **An empty range is refused while parsing**, with curl's two messages, by the option that
   empties it (`CommandLineOption.FlagThatCanRefuse` for the minimums). The provider throws
   `ArgumentException` for one, since no command line can reach it.
4. **In the Schannel build a failed handshake with a ceiling of TLS 1.0 or 1.1 reports
   `failed to receive handshake`** whatever security status Schannel returned, because that is
   what curl's Schannel build printed for every such range measured, and `SslStream`'s statuses
   there describe .NET's hello, not curl's. A socket error is still `Recv failure`. The OpenSSL
   build needs nothing: its error string is already curl's.
5. **The HTTPS proxy gets `--proxy-tlsv1` only.** `TlsClientOptionsMapping.ProxyFromCommandLine`
   maps `ProxyMinimumTlsVersion` to the proxy's minimum and leaves its ceiling unset.

## Consequences

- `--tls-max`, `-1`, `--tlsv1.0` and `--tlsv1.1` now change what the handshake offers, and each
  measured failure prints curl's bytes on both builds; the Windows handshake tests run under
  `[OSCondition(OperatingSystems.Windows)]`, the OpenSSL ones under
  `[OSCondition(OperatingSystems.Linux)]` (macOS's `SslStream` is not OpenSSL).
- Where the operating system will not negotiate TLS 1.0 or 1.1 but an official curl build does
  (curl.se's LibreSSL Windows build against a TLS 1.0-only server), Curl still fails; BL-714's
  hand-built TLS client closes that gap.
- The OpenSSL build's "minimum above a ceiling read before it" refusal is assumed to match the
  Schannel build's, since both come from curl's shared tool code.
