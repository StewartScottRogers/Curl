# ADR-1071 — Curl.Api and Curl.Mcp transfers reach the extensions; Libcurl stays without them

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4098.

## Context

ADR-0999 wired Curl's extensions into `Curl.Console` alone. `Curl.Api` and `Curl.Mcp` run their
transfers on a `ManagedCurlEngine`, which had no public way to take extra protocol handlers, so a
program or an AI agent could not reach a scheme the `curl` tool serves. `Libcurl` mirrors real
libcurl, which has no extensions.

## Decision

- `ManagedCurlEngine` gains a public constructor taking a factory of extension handlers
  (`Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>?`). The production dispatcher passes it
  to `EngineComposition.CreateProtocolHandlers`, which adds the handlers after curl's own, as for the
  `curl` tool. The engine still references no extension project; the host passes the factory in.
- `CurlClient.Create(options)` and the MCP host's registry pass `ExtensionProtocolHandlers.Create`,
  unless `CURL_EXTENSIONS` is `0` (read through `ExtensionProtocolHandlers.For`, the switch the
  `curl` tool already uses), when they pass none and an extension's scheme is refused as any unknown
  scheme (`CURLE_UNSUPPORTED_PROTOCOL`). The MCP host reads the variable through the same injected
  reader as its other variables. `CurlClient.Create(engine, options)` keeps the engine the caller gives.
- `Curl.Api` and `Curl.Mcp` reference `Curl.Extension.Composition.UnitLibrary`, as hosts may
  (ADR-0999). `Libcurl` references no extension project and its transfers reach none.

## Consequences

A transfer through `Curl.Api` or the MCP `transfer` tool reaches the same schemes as the `curl` tool,
and `CURL_EXTENSIONS=0` turns them off in all three. `Libcurl` stays a drop-in for real libcurl.
