# ADR-0673 — `CURLOPT_CURLU` replaces `CURLOPT_URL` when a perform starts

- **Status:** Accepted
- **Date:** 2026-10-17

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2628.

## Context

`Libcurl` stored `CURLOPT_CURLU` as `EasyNetworkOptions.UrlHandle` (ADR-0590), but the engine never read it, so
upstream case 658 (a handle set after a `CURLOPT_URL` of `http://www.example.com`) resolved `www.example.com`
and failed with code 6, and case 659 (a handle with only scheme, host and port, through a proxy) sent nothing.
curl 8.22.0's `Curl_pretransfer` in `lib/transfer.c` replaces `STRING_SET_URL` with
`curl_url_get(data->set.uh, CURLUPART_URL, ..., 0)` whenever the handle is set, and fails with
`CURLE_URL_MALFORMAT` and `No URL set` when that get fails.

## Decision

- The engine applies it, as it applies `CURLOPT_PORT` (ADR-0672): `ManagedEasyTransfer`'s perform from the
  options runs with `UrlHandleOverride.Apply`, which reads the handle's whole URL with no flags and transfers
  it in place of `Url`, before `UrlPortOverride` so a `CURLOPT_PORT` still wins as in libcurl.
- The handle is read when the perform starts, not when the option is set, as libcurl keeps the pointer: a part
  set on the handle after `curl_easy_setopt` reaches the transfer.
- A handle whose URL cannot be read (no host, no scheme) leaves no URL, so the perform answers
  `CurlCode.UrlMalformat` and `No URL set`, libcurl's answer.

## Consequences

Cases 658 and 659 pass and hold the ratchet. A get with no path gives `/`, so case 659's proxy request target
is `http://www.example.com/`. The share handle's DNS key (`ShareHostKeyOf`) still reads `CURLOPT_URL`. The
tests are `UrlHandleOverrideTests` and `ManagedEasyTransferTests.UrlHandle.cs`.
