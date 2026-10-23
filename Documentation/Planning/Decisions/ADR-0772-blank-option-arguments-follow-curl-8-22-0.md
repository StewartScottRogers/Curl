# ADR-0772 — Blank option arguments follow curl 8.22.0, with c-ares and ECH options as a build that has them

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3183
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0 reads each option's argument with `getstr(..., ALLOW_BLANK)` or `getstr(..., DENY_BLANK)`, so an
empty argument (`''`) is accepted by some options and refused with `blank argument where content is expected`
(exit 2) by others. Every option `curl --help all` shows with an argument was run once as
`curl -m 5 --<name> '' http://127.0.0.1:1/` through `Record-CurlExchange.ps1 -NoServer` on 2026-10-22 with the
local Schannel curl 8.22.0 (the lists are in BL-3183's Notes). Curl refused `--proxy-key ''` and `--proxy-pass ''`
as blank where curl accepts them.

That curl has neither c-ares nor ECH, so `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`,
`--dns-ipv6-addr` and `--ech` are refused as `the installed libcurl version does not support this` whatever
their value. Curl acts as a build that has both (BL-643), and accepts any non-empty value for them.

## Decision

- `--proxy-key` and `--proxy-pass` accept an empty argument and store it as given, as curl 8.22.0 does;
  `--proxy-key` keeps the looks-like-a-flag warning of a file name option.
- The c-ares and ECH options keep refusing an empty argument as blank: that is what a curl 8.22.0 built with
  c-ares and ECH does (both read their argument with `DENY_BLANK`), and Curl already acts as that build for
  every other value.
- `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs` pins every argument-taking option in the table, and
  fails when a row is added that is in none of its lists.

## Consequences

A new option row needs its blank behaviour measured and added to one of the test's lists. Options that
are Curl's own (`--log-level`, `--log-file`, `--httpsig-*`) are listed apart, unmeasured.
