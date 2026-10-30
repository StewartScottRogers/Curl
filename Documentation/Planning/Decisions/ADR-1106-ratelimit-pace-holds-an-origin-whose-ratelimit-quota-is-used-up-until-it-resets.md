# ADR-1106 — --ratelimit-pace holds an origin whose RateLimit quota is used up until it resets

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4139.

## Context

BL-4139 adds the RateLimit header fields as an extension (ADR-0999) in `Curl.Extension.RateLimit.UnitLibrary`: an
option that reads `RateLimit-Policy` and `RateLimit` and waits accordingly. curl has no such option, so the extension
is tested against draft-ietf-httpapi-ratelimit-headers-11 (23 May 2026), the latest revision and the version
followed, and never changes what curl does without the option.

## Decision

- **Option.** One flag, `--ratelimit-pace`, with no argument; curl has no option of that name (`--rate` and
  `--limit-rate` pace by count and by bytes, not by the server's fields). It is a transfer hook (ADR-1007),
  registered in the option, description and transfer hook catalogues; it has no URL scheme, so, as ADR-1094's
  `--content-digest`, it adds no line to `ExtensionCatalogue`.
- **One hook per option group.** The host activates a group's hooks once and runs every transfer of the group through
  them, `--retry` attempts and `-Z` parallel transfers included, so the hook keeps one hold time per origin
  (scheme, lower-cased host, port) for the group. Origins are kept apart because §7 scopes a quota to the server
  that sent it.
- **When to hold (§4.1.1, §4.1.2, §3.1.3).** After a transfer whose report has response fields, each `RateLimit`
  member whose available quota `r` is 0 holds the origin for its effective window `t` seconds, or, with no `t`, for
  the window `w` of the `RateLimit-Policy` member of the same name. The longest hold of the response wins, and a
  parallel transfer that ends later with a shorter hold never shortens one already set. A member with quota left
  changes nothing: §8.3 says available units are not granted requests, so the hook only stops sending into a quota
  the server says is empty and never spreads requests over a window.
- **How to wait (§7).** Before a transfer to a held origin, the hook waits on the transfer's `TimeProvider` until the
  hold has passed, ended by the transfer's cancellation (`-m`). A hold is capped at 4,294,967 seconds (about 49.7
  days), the longest `Task.Delay` takes.
- **Parsing.** Each field is a Structured Fields List (RFC 9651 §3.1); every field line of one name is joined with
  commas. A member whose name is not a String (§3, §4) is skipped, and a parameter whose value is not a non-negative
  Integer (`pk`'s Byte Sequence, a negative number) is left out, so one bad member never hides the others. Quoted
  names may hold commas and semicolons; escaped quotes inside a name are not unescaped.
- **Not HTTP.** A transfer that is not HTTP runs unchanged and holds nothing.

## Left out, and why

- **Partition keys (§3.1.4, §4.1.3, §7.1).** A partition key is opaque to the client and a single curl run is one
  client, so holds are kept per origin, not per key.
- **Quota units (§3.1.2) and the quota `q`.** Only an empty quota holds; pacing by units the client cannot count
  (content bytes, concurrent requests) would guess what the server means.
- **Problem types (§5).** The `quota-exceeded` problem documents carry what the fields already say; reading response
  bodies would mean buffering them.
- **A message on stderr while waiting.** Transfer hooks have no verbose channel; `-v` still shows the fields.

## Consequences

- `curl --ratelimit-pace URL1 URL2` waits before `URL2` when `URL1`'s response said the quota of their origin is used
  up; without the option, or with `CURL_EXTENSIONS=0` (when the option is refused as unknown, exit 2), every transfer
  is unchanged.
- `curl --extensions` and `curl --ai-help extensions` list it.
