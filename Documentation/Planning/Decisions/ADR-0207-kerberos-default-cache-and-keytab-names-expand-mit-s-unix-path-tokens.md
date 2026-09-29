# ADR-0207 — The default cache and keytab names honour krb5.conf and expand MIT's Unix path tokens

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-816.
Builds on ADR-0158, which found the defaults from the environment and MIT's built-in names
only.

## Context

MIT Kerberos finds the default credential cache as `KRB5CCNAME`, then `[libdefaults]
default_ccache_name`, then `FILE:/tmp/krb5cc_<uid>`, and the default keytab as
`KRB5_KTNAME`, then `default_keytab_name`, then `FILE:/etc/krb5.keytab`. Both
`krb5.conf` values go through MIT's parameter expansion (`expand_path.c`), which replaces
`%{token}` parameters. BL-789 had already added `default_ccache_name` with `%{uid}` and
`%{euid}` only; the keytab ignored `krb5.conf` altogether.

## Decision

- **Both stores read their `krb5.conf` value** and expand it through one
  `KerberosPathExpansion`; the environment variable still wins and is never expanded, as
  in MIT.
- **Tokens:** `%{uid}`, `%{euid}` and `%{USERID}` give the user id (curl never runs
  setuid, so the real and effective ids agree); `%{username}` gives an injected
  `readUserName`, by default `Environment.UserName` (the BCL's `getpwuid(geteuid())` off
  Windows); `%{TEMP}` gives `TMPDIR` when set, else `/tmp`, as MIT's
  `expand_temp_folder`; `%{LIBDIR}`, `%{BINDIR}` and `%{SBINDIR}` give MIT's default
  `/usr/local` prefix, since no distribution's compiled-in path can be known; `%{null}`
  gives nothing. Tokens match case-sensitively, as MIT's `strncmp` does.
- **An unclosed `%{` or an unknown token throws** `KerberosFileException` with
  `KerberosFileError.PathTokenInvalid`, MIT's `EINVAL`. What a token gives is not
  expanded again.
- **Windows-only MIT tokens** (`%{APPDATA}` and the like) are unknown: the hand-built
  Kerberos route runs off Windows (ADR-0142).
- `KeytabStore` takes a required `Func<uint> readUserId`, as `CredentialCacheStore` does.

## Consequences

- A `krb5.conf` naming `FILE:%{TEMP}/krb5cc_%{uid}` or `FILE:/etc/%{username}.keytab`
  resolves as MIT resolves it.
- Debian's and Fedora's `KEYRING:persistent:%{uid}` expands, then falls to ADR-0142's
  system GSS-API route, since the hand-built route reads no keyring.

## Alternatives considered

- **Expand only `%{uid}` and `%{euid}`** (BL-789's reach). Simpler, but a `%{TEMP}` or
  `%{username}` value would name a file that does not exist; rejected.
- **Leave an unknown token in place.** MIT fails instead, and a literal `%{x}` path is
  never what the administrator meant; rejected.
