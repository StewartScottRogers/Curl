# ADR-0454 — The DNS options and `--ech` stay supported on Windows; `--no-proxy-http3` is accepted

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1829, GF-0036).

## Context

Finding GF-0036 measures six option items as a difference from curl 8.21.0's Schannel
reference build on Windows:

- `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr` and `--ech`: the
  Schannel build refuses each with exit 2, `curl: option <name>: the installed libcurl version does
  not support this`, because it is built without c-ares and without ECH. Curl accepts all five on
  every platform: ADR-0170 resolves the DNS options through a hand-built c-ares-style client, and
  ADR-0151 and ADR-0327 run `--ech` on the hand-built TLS client.
- `--no-proxy-http3`: measured on this machine on 2026-10-08, `curl.exe --no-proxy-http3` (8.21.0
  Schannel) prints `curl: (2) no URL specified`, and `curl.exe --no-proxy-http3 -s
  http://127.0.0.1:1/` exits 7, while `--proxy-http3` is refused as not supported. curl checks
  the feature only when the flag is turned on. Curl had no row for `proxy-http3`, so it refused
  both spellings (ADR-0137).

## Decision

1. `--proxy-http3` gets a row, built with `CommandLineOption.UnsupportedFlagTurnedOffQuietly`:
   `--proxy-http3` is still refused on every build, and `--no-proxy-http3` is accepted and changes
   nothing. `--ai-help` still says the option is not supported.
2. `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr` and `--ech` stay
   accepted and working on every platform. These five GF-0036 items are an intended difference:
   the gap analysis office should record them as `excluded`, with this ADR as the reason, rather
   than expect `match`. A lane may not write under `Gap/`, so that exclusion is left to an
   interactive session.

## Why

- Matching the platform's curl decides `--no-proxy-http3`: the Schannel build accepts it.
- The standing rule of 2026-09-28 (root `CLAUDE.md`, "Decisions") is a complete
  reimplementation: a feature is never left out because the reference build lacks it. curl's
  c-ares and ECH-enabled builds accept these five options, so a script written for them works
  with Curl. Refusing them on Windows would throw away ADR-0170's and ADR-0327's work to match
  one build's missing libraries. The `-V` lines already follow the Schannel build (ADR-0450 to
  ADR-0453); the options themselves keep working, as `--http3` does (ADR-0451).

## Consequences

- A later gap analysis measures `options:--proxy-http3:no-form` as `match`.
- The other five GF-0036 items keep measuring as a difference until the office excludes them.
