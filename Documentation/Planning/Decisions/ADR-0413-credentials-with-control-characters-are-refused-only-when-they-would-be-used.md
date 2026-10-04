# ADR-0413 — Credentials with control characters are refused only when they would be used

- Status: Accepted
- Date: 2026-10-03
- Task: BL-1411
- Decided by Claude under Stewart's delegation.

## Context

curl 8.21.0's `lib/url.c` refuses a URL user name or password that percent-decodes to a byte
below 0x20 (exit 3, `error extracting credentials from URL`; only 0x00 for `http`, `https`, `ws`
and `wss`), and a matching `.netrc` entry whose login or password holds one (exit 26,
`control code detected in .netrc credentials`; never for those four schemes). Measured
2026-10-03 (BL-1411 Context). Three points the measurements did not settle had to be decided.

## Decision

1. The URL's credentials are checked only when `-u` gives no user name. curl decodes them only
   when no user name was set by option (`STRING_USERNAME`), so a `-u q:r` beside `ftp://u%01x@host/`
   logs in as `q` and is not refused.
2. The netrc check applies under `--netrc-optional` as under `-n`: curl's `override_login` runs it
   on any entry found, whatever the option, and only a missing or malformed file is forgiven.
3. A redirect hop's own URL credentials are checked the same way, after its proxy is chosen
   (`TransferCredentialLookup.TryCheckUrlCredentials` in the hop proxy selector), because curl
   parses each hop's URL as it parsed the first. A hop with no credentials of its own keeps what it
   carried before.

## Consequences

`TransferCredentialLookup` holds every rule; `CurlCommandRunner` writes the `-v` info line for
the first URL's refusal. A hop's refusal under `-v` writes no info line yet; nothing measured
pins it.
