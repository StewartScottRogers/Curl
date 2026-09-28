# ADR-0101 — The redirect follower sends the auto referer and reports the last one sent

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

`-e "<url>;auto"` turns on curl's autoreferer: each redirect `-L` follows is sent with the
URL it came from as its `Referer`, and `CURLINFO_REFERER`, which `%{referer}` prints, is the
`Referer` of the last request. BL-251 parsed the flag into `CommandLineOptions.AutoReferer`
but nothing read it, and BL-305 (ADR-0060) printed `%{referer}` from the `-e` text, which
ADR-0015 listed as known before the transfer and so not a `TransferReport` member.

Measured with curl 8.21.0 (mingw, Schannel) through `Record-CurlExchange.ps1`, every command
and byte in BL-361's Notes: with `;auto -L`, hop N+1 sends hop N's URL without user
information or fragment, query kept, and `%{referer}` prints the last hop's `Referer`; without
`-L` it prints the `-e` text before `;auto`, or nothing.

## Decision

1. **`HttpRequestOptions.AutoReferer` carries the flag, and `RedirectFollower` applies it.**
   Each followed hop's `HttpRequestOptions.Referer` is the previous hop's URL written as
   `scheme://host[:port]path[?query]`: user, password and fragment dropped, the port only
   when it is not the scheme's default. *Why:* the follower is the only place that knows the
   previous URL, and the HTTP handler already sends `Referer` from the options, so no handler
   changes.
2. **`TransferReport.Referer` is the `Referer` the last dispatched request was sent with**,
   set by the follower for every chain it follows; `null` from a handler, meaning
   `HttpRequestOptions.Referer`. `%{referer}` prints it, else the `-e` text. This amends
   ADR-0015's "What the report does not carry": `%{referer}` is now a report member. *Why:*
   under `;auto` it is known only after the chain, as `%{url_effective}` is, and it follows
   the same "`null` from a handler" convention as `EffectiveUrl`.
3. **A refused redirect (`--max-redirs`, an unparsable or refused target) reports the
   `Referer` of the last request actually sent.** Not measured: curl sets its referer inside
   `Curl_follow` after the limit check, so an unparsable target may already show the new one.
   *Why:* the simplest rule that is right for every measured case; a later task measures it
   if a script needs it.

## Consequences

- `-e ";auto" -L` now sends the `Referer` header on every followed hop, as curl does.
- A `TransferReport` from a handler is unchanged; only the follower sets `Referer`.
- The port rule for a URL typed with its default port (`http://h:80/`) is unmeasured.
