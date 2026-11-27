# ADR-1454 — Kept `--doh-url` resolvers belong to the connection cache

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1453, under which each library handle owned its kept resolver

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6067.

## Context

ADR-1453 gave each `ManagedEasyTransfer` its own `KeptDohResolver`, so handles in one multi handle,
or sharing connections through a share handle, each opened their own DoH connections. libcurl keeps
its DoH sub-transfers' connections in the connection cache the transfer uses: the easy handle's,
the multi handle's or the share handle's.

## Decision

- Each connection cache of the managed engine has one `KeptDohResolver` beside it: each library
  handle's own, each multi handle's, each share handle's and the engine-wide one. A perform takes
  the one beside the connection cache it takes.
- It keeps one resolver for each set of options one was built from: every TLS option, and every
  network option but `CURLOPT_URL`, `CURLOPT_CURLU`, `CURLOPT_PORT` and `CURLOPT_CONNECT_TO`, the
  `CURLOPT_RESOLVE` entries compared one by one. Transfers with different settings over one cache
  no longer close each other's connections, as they would if one resolver replaced another.
- At most four resolvers are kept beside one cache. A fifth closes the one asked for longest ago,
  which bounds the connections of an application that changes a setting before every perform. A
  transfer still asking a closed resolver finishes; the resolver closes each connection after use.
- The cache's owner closes them: `curl_easy_cleanup` its own, `curl_multi_cleanup` and
  `CURLMNWC_CLEAR_CONNS` the multi's, `curl_share_cleanup` the share's, and disposing the engine
  those of every handle, multi and the engine-wide cache.
- `CURLMOPT_MAXCONNECTS` and the connection limits do not count DoH connections.

## Consequences

- A handle cleaned up while in a multi handle, or attached to a share handle that shares
  connections, leaves the DoH connections in that cache open for the others.
- ADR-1453's closing of a replaced resolver at the end of a perform is gone: an earlier setting's
  connections stay until four newer ones push it out or the owner is cleaned up.
- The curl tool is unchanged: its option group's `TransferDispatch` disposes its resolver (ADR-1448).
