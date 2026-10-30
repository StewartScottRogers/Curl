# ADR-1100 — --client-hints sends the low-entropy User-Agent Client Hints to trustworthy URLs

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4141.

## Context

BL-4141 adds browser Client Hints as an extension (ADR-0999) in `Curl.Extension.ClientHints.UnitLibrary`: an option
that sends the `Sec-CH-UA*` fields a browser sends. curl has no such option, so the extension is tested against
RFC 8942 (February 2021) and the WICG User-Agent Client Hints Draft Community Group Report as published at
<https://wicg.github.io/ua-client-hints/> in October 2026, the versions followed, and never changes what curl does
without the option.

## Decision

- **Option.** One flag, `--client-hints`, with no argument; curl has no option of that name. It is a transfer hook
  (ADR-1007), registered by one line in each of the option, description and transfer hook catalogues; it has no URL
  scheme, so, as ADR-1094's `--content-digest`, it adds no line to `ExtensionCatalogue`.
- **Fields (WICG §3.1, §3.3).** The low-entropy hint table, which a browser sends with every request without an
  opt-in: `Sec-CH-UA` (§3.3.1), `Sec-CH-UA-Mobile` (§3.3.6) and `Sec-CH-UA-Platform` (§3.3.8), each a Structured
  Field (RFC 8941). `Sec-CH-UA` is `"curl";v="8", "Not_A Brand";v="99"`: curl's own brand at the major version of
  curl 8.22.0, the version Curl presents in its `User-Agent`, and a GREASE brand (§3.2), fixed rather than varied so
  the bytes are reproducible. `Sec-CH-UA-Mobile` is `?0`. `Sec-CH-UA-Platform` is `"Windows"`, `"macOS"`, `"Linux"`
  or `"Unknown"` from the running OS.
- **Where (WICG §2, RFC 8942 §4.1).** Only to a potentially trustworthy URL, as a browser sends them only in a secure
  context: `https`, `wss`, or a loopback host (`localhost`, `*.localhost`, `127.0.0.0/8`, `::1`). A plain `http`
  request to any other host gets none.
- **`-H` wins.** A hint field `-H` already gave is not added again, so a user can send `Sec-CH-UA-Mobile: ?1` or any
  other value.

## Left out, and why

- **High-entropy hints** (`Sec-CH-UA-Arch`, `-Bitness`, `-Full-Version-List`, `-Model`, `-Platform-Version`,
  `-WoW64`, `-Form-Factors`) and **`Accept-CH`** (RFC 8942 §3.1): a browser sends those only after an origin asked
  for them in an earlier response and keeps that opt-in per origin. curl keeps no per-origin state between
  invocations, and replaying a request on `Accept-CH` or `Critical-CH` (the Client Hint Reliability draft) would
  change what a transfer sends. A user who wants one sends it with `-H`.
- **A brand taken from `-A`**: `Sec-CH-UA` names curl whatever `-A` says, because a free-form User-Agent has no
  reliable brand and version to read.

## Consequences

`curl --client-hints https://example.com/` sends three more request fields; without the option, or with
`CURL_EXTENSIONS=0` (where the option is refused as unknown, exit 2), every transfer is unchanged.
