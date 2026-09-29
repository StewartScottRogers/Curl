# ADR-0218 — HSTS is one cache per run, read before and written after each `--hsts` transfer

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-621.
Builds on BL-620's `HstsCache` (`Curl.Core.UnitLibrary/Hsts`).

## Context

`--hsts <file>` has to load curl's HSTS cache, switch an `http` URL or redirect to a known host to
`https`, learn from `Strict-Transport-Security` and write the file back. The measurements are in
BL-621's Notes (curl 8.21.0 mingw Schannel, 2026-09-29).

## Decision

1. **One cache per run.** `CurlCommandRunner` holds one `HstsTransferPolicy`, shared by every
   transfer of every option group, `-Z` ones included (it locks), and used with or without
   `--hsts`: curl's tool shares HSTS through its share handle, so a header learned by one transfer
   switches a later URL of the same run. `--hsts` only adds the file.
2. **The file.** Each transfer of a group with `--hsts` reads the file into the cache just before
   the switch and writes the whole cache back after the `-c` jar and the `--alt-svc` file, whatever
   the transfer's outcome. A missing or unreadable file reads as empty, one that cannot be written
   is left alone silently, and `--hsts ""` does neither.
3. **The switch** replaces the scheme only, as typed: `http://host/` goes to 443 by the scheme's
   default, an explicit port stays. `-v` prints `* Switched from HTTP to HTTPS due to HSTS => <url>`
   first, and `%{url_effective}` shows the switched URL.
4. **Redirects.** `RedirectFollower` takes the cache: every hop's response is learned, and an `http`
   target the cache knows is switched after it parses and before `--proto-redir` checks its scheme,
   so the check sees `https`.
5. **Learning** takes every `Strict-Transport-Security` header of a response that came over
   `https`, in order; one over plain `http` is ignored.

## Consequences

curl prints `* Issue another request to this URL: '<target>'` before the switch line of a followed
redirect; Curl prints that line only for the 417 resend today, which BL-907 fixes.
