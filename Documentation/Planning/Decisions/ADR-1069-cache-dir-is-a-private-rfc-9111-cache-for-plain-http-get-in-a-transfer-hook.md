# ADR-1069 — `--cache-dir` is a private RFC 9111 cache for plain HTTP GET, in a transfer hook

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4133.

## Context

BL-4133 adds the extension `Curl.Extension.HttpCache.UnitLibrary` (ADR-0999): `--cache-dir <dir>` stores HTTP
responses on disk, reuses them and revalidates them with `ETag` and `Last-Modified`. Upstream curl has no HTTP cache,
so there is no curl output to match; RFC 9111 (HTTP Caching, June 2022) decides the behaviour. BL-4203 built the
RFC 9111 core (storability, freshness, validation, the on-disk entry); BL-4204 gave transfer hooks the
`ExecuteAsync` seam (ADR-1019) that lets a hook answer a transfer itself or replace the response it received.

## Decision

- **Names.** One option, `--cache-dir <dir>`, and the extension name `cache-dir` in `curl --extensions` and
  `curl --ai-help extensions`. No scheme: the cache rides on curl's own `http` and `https`.
- **Registration.** Like `--jsonrpc`, the extension adds no protocol handler, so `ExtensionCatalogue.cs` gets no
  line: it holds protocol handler factories. Its option, description and hook activator are one line each in
  `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue`. With
  `CURL_EXTENSIONS=0` the option is not registered, so curl refuses it as an unknown option (exit 2).
- **Specification followed:** RFC 9111, as a private cache (§1): `private` responses and responses to requests
  with `Authorization` may be stored, since the directory belongs to one user.
- **Which requests.** Only a plain GET of the whole resource: an HTTP transfer with no custom method (`-X`), body
  (`-d`, `-F`), upload (`-T`), `HEAD` (`-I`), range (`-r`), resume (`-C`) or curl's own time condition (`-z`). Every
  other transfer runs exactly as without the option. The cache key is `GET` and the target URI without user
  information or fragment (§2).
- **Fresh** (§4.2): a stored response that is fresh, and a request without `Cache-Control: no-cache` (§5.2.1.4), is
  answered from the directory without contacting the origin; its status line and header fields go to `-i` and
  `-D`, its body to the output, and `-w` sees its status code, header fields and content type.
- **Stale** (§4.3): the request is sent with `If-None-Match` and `If-Modified-Since` from the stored validators
  (§4.3.1). A 304 that selects the stored response (§4.3.3) updates its header fields (§4.3.4), is stored again, and
  the stored response is written in the 304's place with its status code; a 304 that does not select it, and any
  other response, is written as received.
- **Storing** (§3): a successful response is stored when `ResponseStorability` allows it and it has no `Vary`.
- **A cache that cannot be written** (an I/O error) leaves the transfer as it was: the response is still written.
- **The response is buffered.** On a cacheable request the body and headers are captured, then written, so a
  cacheable download reaches the output when it ends rather than as it arrives.

## Left out, and why

- **`Vary` secondary keys (§4.1).** A response with `Vary` is not stored rather than stored under its selecting
  header fields: correct (never a wrong response reused) and simple; secondary keys can come as a later task.
- **Serving stale on error (§4.2.4), `stale-while-revalidate` and `stale-if-error`.** curl's job is to report the
  origin's answer; a stale response stands in only after a 304 confirmed it.
- **Invalidation on unsafe methods (§4.4).** Those requests pass through untouched, and a later GET revalidates by
  freshness; invalidation is a cache shared by many clients' concern more than one command line's.
- **Partial content (§3.3, §3.4).** Ranged and resumed transfers are not cached, so 206 responses are never combined.
- **HEAD (§4.3.5).** Not answered from or used to update stored GET responses.

## Consequences

Without `--cache-dir` no hook is activated and every transfer is unchanged. The extension is tested against
RFC 9111's sections with a stand-in for curl's handler that writes recorded responses, never against the network,
in `Curl.Extension.HttpCache.UnitTests`.
