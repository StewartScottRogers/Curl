# ADR-1453 — A library handle keeps its `--doh-url` resolver from perform to perform

- **Status:** Accepted
- **Date:** 2026-11-26
- **Amends:** ADR-1448, under which each perform of the managed engine disposed the resolver it built

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6065.

## Context

Each perform of `ManagedEasyTransfer` builds new transports, and ADR-1448 disposed their
`DohDnsResolver` when the perform ended, so a handle reused no DoH connection. libcurl adds its DoH
sub-transfers to the multi handle the transfer runs on (`lib/doh.c`), which for `curl_easy_perform`
is the easy handle's own, so their connections stay in the handle's connection cache. How the curl
tool reuses DoH connections within one connection cache was measured for ADR-1441. A libcurl program
performing twice on one handle was not measured: no libcurl development files are on the machine.

## Decision

- Each library handle owns a `KeptDohResolver`. Every set of transports its performs build takes
  its `--doh-url` resolver from it, so ADR-1441's reuse rules apply from one perform to the next.
- The resolver is kept while the options it was built from are unchanged: every TLS option, and
  every network option but `CURLOPT_URL`, `CURLOPT_CURLU` and `CURLOPT_PORT`. Any other change
  builds a new one, and the replaced one closes its connections when that perform ends. This is
  stricter than matching only the DoH settings, which costs at most a new connection.
- A perform without `--doh-url` leaves the kept resolver alone.
- `curl_easy_reset` keeps it, and `curl_easy_cleanup` (`DisposeAsync`) closes its connections.
  `curl_easy_duphandle` starts with none.
- The handle keeps it whether or not it is in a multi handle or attached to a share handle.

## Consequences

- A DoH connection stays open until its handle is disposed. Disposing only the engine does not close
  it.
- Handles in one multi handle, or sharing connections through a share handle, do not reuse each
  other's DoH connections, where libcurl's shared connection cache would let them.
- The curl tool is unchanged: its option group's `TransferDispatch` disposes its resolver (ADR-1448).
