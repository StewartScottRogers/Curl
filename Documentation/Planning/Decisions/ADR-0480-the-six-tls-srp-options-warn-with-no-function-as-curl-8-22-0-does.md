# ADR-0480 — The six TLS-SRP options warn with no function, as curl 8.22.0 does

- **Status:** Accepted
- **Date:** 2026-10-13
- **Supersedes:** the option parsing of ADR-0229 and ADR-0328, and the TLS-SRP rows of ADR-0397

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-2142
(audit finding AF-0183).

## Context

ADR-0479 retargets Curl from curl 8.21.0 to curl 8.22.0, which drops TLS-SRP. Measured on
2026-10-13 against `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel`, with the URL
`http://127.0.0.1:1/`:

- `--tlsuser`, `--tlspassword`, `--tlsauthtype`, `--proxy-tlsuser`, `--proxy-tlspassword` and
  `--proxy-tlsauthtype` each print `Warning: --<name> is deprecated and has no function anymore`
  and the transfer runs (exit 7 here, exit 0 against a live server). Any value is taken, empty
  included, and `--tlsauthtype 1` is not refused.
- `-s` before the option drops the warning.
- `--no-tlsuser` and the others are refused as not reversible, and the option as the last
  argument is refused as `requires parameter`, exit 2.
- `curl --help all` still lists all six.

Curl refused all six on Windows with exit 2 (`RefusedBySchannelBuild`, ADR-0397), so a script
could tell the two binaries apart.

## Decision

The six options are `CommandLineOption.NoFunctionValue` rows, like `--egd-file`, on every
platform. The warning comes from curl's tool code rather than a TLS backend, so the OpenSSL
builds on Linux and macOS behave the same. `CommandLineOptions`' six TLS-SRP properties are
get-only and always `null` until BL-2152 removes them, their Console mapping and the hand-built
TLS-SRP client. `--help` keeps listing them, and `--ai-help` no longer says Windows refuses them.

## Consequences

- AF-0183's reproduction now exits 0 for both options, as curl does.
- TLS-SRP can no longer be used through Curl. BL-2152 removes the code nothing reaches now and
  checks whether the OpenSSL build's `Features:` line still lists `TLS-SRP`.
