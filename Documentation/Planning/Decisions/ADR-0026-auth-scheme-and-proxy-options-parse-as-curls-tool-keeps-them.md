# ADR-0026 — The auth-scheme and proxy options parse as curl's tool keeps them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-192 parses `--basic`, `--digest`, `--anyauth`, `--oauth2-bearer`, `-x`/`--proxy`,
`-U`/`--proxy-user`, `--noproxy`, `-p`/`--proxytunnel` and `--socks4`, `--socks4a`,
`--socks5`, `--socks5-hostname` into `CommandLineOptions`. ADR-0014 says `--anyauth` maps to
`HttpAuthSchemes.Any`, which leaves Bearer out, and left open how the scheme options combine.
ADR-0022 found that `--oauth2-bearer` starts the wanted set from nothing.

Measured with the reference build (ADR-0018: curl 8.21.0, mingw) on 2026-09-26,
`Record-CurlExchange.ps1` against a loopback server answering 401 with
`WWW-Authenticate: Bearer realm="x"` and `WWW-Authenticate: Basic realm="x"`:

| Arguments | Authorization sent |
| --- | --- |
| `-u u:p --no-basic` | `Basic dTpw` at once |
| `-u u:p --digest --no-digest` | `Basic dTpw` at once |
| `-u u:p --basic --no-basic --digest` | nothing, no second request |
| `-u u:p --digest --basic --no-basic` | nothing, no second request |
| `-u u:p --oauth2-bearer tok --basic` | nothing, then `Bearer tok` |
| `-u u:p --anyauth --basic` | nothing, then `Basic dTpw` |
| `-u u:p --anyauth` | nothing, then `Basic dTpw` |
| `--oauth2-bearer tok --anyauth` | nothing, then `Bearer tok` |
| `--oauth2-bearer tok --no-basic` | `Bearer tok` at once |

And, reading the first bytes the proxy listener received: `--socks5 A -x B` speaks HTTP;
`-x A --socks5 B` and `--socks4 A -x socks5://B` speak SOCKS5; `--socks5 http://A` speaks
HTTP; `-x socks://A` and `--socks4 A` speak SOCKS4. `-x bogus://h:1`, `-x socks6://h:1`,
`-x ftp://127.0.0.1:1` and `--socks5 bogus://h:1` read the whole command line and then fail
the transfer with exit 7, `curl: (7) Unsupported proxy scheme for '<value>'`.

## Decision

- **Scheme options keep curl's bit set.** `--basic` and `--digest` add their scheme and
  their `--no-` spellings remove it; `--anyauth` replaces the set with every scheme,
  Bearer included; `--oauth2-bearer` adds Bearer. `CommandLineOptions.AuthSchemes` reads
  that set with Bearer dropped when there is no token, and gives `Basic` when nothing is
  left. So `--anyauth` alone is `Any`, as ADR-0014 wrote, and `--oauth2-bearer tok --anyauth`
  is `Any | Bearer`, which is what lets the authenticator answer the Bearer challenge as the
  reference does.
- **One proxy slot.** `-x` and the four `--socks` options all set one
  `CommandLineProxy`: the value as given and the kind the option names. The last one wins,
  value and kind together, and a scheme in the value outranks the option's kind. `-x ''`
  is kept as an empty address: no proxy at all.
- **An unsupported scheme is a transfer failure, not a refusal.** It is not refused at
  parse time with exit 2; `CommandLineProxy.TryGetKind` returns the exit-7 `TransferResult`
  for the transfer to report. Host, port, user information and syntax errors (exit 5) are
  the proxy selector's (ADR-0024).
- **`-U` prompts as `-u` does**, with `Enter proxy password for user '<user>':`, after the
  host prompt; `--oauth2-bearer` suppresses the host prompt, as measured.

## Consequences

- `CommandLineOptions` carries `AuthSchemes`, `BearerToken`, `Proxy`, `ProxyCredentials`,
  `NoProxy` and `ProxyTunnel`; `Curl.Console` maps them onto `HttpRequestOptions` and the
  proxy selector when it wires HTTP authentication and proxies.
- The scheme is read in two places until the console wiring passes the option's kind to
  the proxy selector; a follow-up task reconciles them.

## Alternatives considered

- **`--anyauth` as `Any` without Bearer, always.** Matches ADR-0014's table, but
  `--oauth2-bearer tok --anyauth` would then send nothing after a Bearer challenge where the
  reference sends `Bearer tok`.
- **Refuse an unknown proxy scheme at parse time.** Simpler, but exit 2 where curl exits 7,
  and it would stop `-x bogus://h` with no URL from reporting the no-URL refusal first.
