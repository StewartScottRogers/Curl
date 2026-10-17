# ADR-0642 — A serial transfer's TLS, authentication and proxy options go through `curl_easy_setopt`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2492.

## Context

ADR-0631 moved the behaviour, transfer and network groups of a serial transfer's `EasyOptions` onto
`CurlEasy.SetOption` and left the other groups whole on the engine transfer's hooks. Upstream's `config2setopts.c`
sets the TLS, authentication and proxy options with one `curl_easy_setopt` call each too. Libcurl's setters
(`Libcurl/Options/Tls`, `Authentication`, `Proxy`) refuse, ignore or widen some values, and the output must stay
byte for byte what it is.

## Decision

1. `EasySetoptsFromCommandLine.Of` also makes one `EasySetopt` per TLS, authentication and proxy field with a
   `CURLOPT_*` whose value differs from libcurl's default, after the network group: the origin's twenty TLS options,
   their twenty `PROXY_*` twins, then the handle's (`SSL_VERIFYSTATUS`, `CERTINFO`, `SSL_EC_CURVES`,
   `SSL_SIGNATURE_ALGORITHMS`, `SSL_SESSIONID_CACHE`, `ECH`, `DOH_SSL_*`), the authentication rows and the proxy rows.
   `SSLVERSION` carries the minimum and `--tls-max`'s maximum in one value; `SSL_VERIFYHOST` and
   `DOH_SSL_VERIFYHOST` are `2L` when on, as curl's tool sets them; the `CURLAUTH_*` masks go as their unsigned 32-bit
   value; `PROXYHEADER` as a `string[]`.
2. Order follows what the setters do: `CURLOPT_AWS_SIGV4` comes before `CURLOPT_HTTPAUTH` (its setter also sets
   `HTTPAUTH` to `CURLAUTH_AWS_SIGV4`, which the record's own mask then replaces), and `CURLOPT_HAPROXY_CLIENT_IP`
   before `CURLOPT_HAPROXYPROTOCOL` (its setter turns the protocol on, as libcurl's does; the engine already sends the
   PROXY header for a client IP alone, so the transfer is unchanged).
3. `--ech` goes as curl's tool sends it: the mode, then `pn:<name>`, then `ecl:<list>`, each its own `CURLOPT_ECH`.
4. These stay on the hooks (`EasySetoptsFromCommandLine.KeptOnHooks`):
   - `Engine` (`--engine`): `CURLOPT_SSLENGINE` answers `CURLE_NOT_BUILT_IN` for any name, which would end the
     transfer with the setopt text; the engine's own setup failure already prints what curl 8.21.0 prints.
   - `EngineIsDefault` and `FalseStart`: their setters answer `CURLE_NOT_BUILT_IN`; the command line never sets them.
   - `CertificateAuthorityCacheTimeout`: the command line never moves it, and its setter works in whole seconds.
   - An `--ech` mode other than `false`, `grease`, `true` or `hard`, or a `pn:`/`ecl:` value shorter than two
     characters: `CURLOPT_ECH` would ignore it, while the performer's `--ech` refusal needs to see it.
   - The TLS-SRP fields (`TlsAuth*` and their proxy twins): their type setter refuses anything but `SRP`, and the
     command line keeps no value for them (ADR-0516).
   - Curl's `--httpsig-*` options: libcurl has no `CURLOPT_*` for them.
5. A refused call ends the transfer as ADR-0631 decided. No command line reaches one: every TLS, authentication and
   proxy value the parser accepts is one its setter keeps (`--proxy-tlsv1` and `--tlsv1.x` map to known versions,
   `--tls-max` to a known maximum, the proxy types and `CURLAUTH_*` masks to values the setters take).

## Consequences

- The hooks keep only the HTTP and protocol groups whole; BL-2493 moves them.
- `CurlCommandRunnerEasyTransferTests` pins `-k`, `-u` and `--noproxy` reaching the handle through `SetOption` and not
  the hooks; `EasySetoptsFromCommandLineTests` pins the rows, the order and what stays on the hooks.
- Libcurl's setters were used as they are; none needed a change.
