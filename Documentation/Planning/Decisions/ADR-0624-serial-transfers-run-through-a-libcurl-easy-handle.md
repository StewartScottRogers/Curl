# ADR-0624 — Serial transfers run through a Libcurl easy handle

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2451.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` re-bases `Curl.Console` onto `Libcurl`, so the tool is
libcurl's client as upstream's is (ADR-0506). Before BL-2451 the runner created each serial transfer's handle with
`ICurlEngine.CreateEasy` and drove the engine's `IToolEasyTransfer` directly (ADR-0552, ADR-0622). Two things stood
in the way of a plain `CurlEasy`:

1. The runner still needs the tool's hooks (`ToolSettings`, `Host`, `ToolInputs`, `ToolOutputs`, `ToolEvents`,
   `ToolProgress`, `ToolDispatch`) and the attempt's whole `LastResult`, which libcurl's options have no `CURLOPT_*`
   for, and `CurlEasy` keeps its engine transfer internal to `Libcurl`.
2. Turning every field of `EasyOptionsFromCommandLine.EasyOptionsOf` into a `curl_easy_setopt` call is about 150
   options across eight groups, each to be checked against Libcurl's setters (range checks, clamps, list copies) with
   the output byte for byte unchanged: more than one task run.

## Decision

1. `Curl.Console`'s `ToolCurlEngine` wraps the runner's `ICurlEngine` and initialises a `CurlLibrary` over itself
   (`curl_global_init`, once per runner). Its `EasyInit` is `curl_easy_init` on that library, and it pairs the new
   `CurlEasy` with the `IToolEasyTransfer` its own `CreateEasy` just made, as a `ToolEasyHandle`. Every other engine
   call passes straight through. No `Libcurl` member is opened to the tool: the hooks are the curl tool's private
   wiring, as upstream's tool passes its own callbacks and user data.
2. A serial transfer creates one `ToolEasyHandle`, sets the option group's records on the hooks' `Options`, sets
   `CURLOPT_URL` through `CurlEasy.SetOption`, performs each attempt, `--retry` attempts included, with
   `CurlEasy.PerformAsync` (`curl_easy_perform`), reads the attempt's result from the hooks' `LastResult`, and
   disposes the handle (`curl_easy_cleanup`) when the transfer ends.
3. The remaining groups move to `SetOption` calls in three follow-up tasks: BL-2491 (behaviour, transfer, network),
   BL-2492 (TLS, authentication, proxy) and BL-2493 (HTTP, protocols). A field with no `CURLOPT_*` stays on the hooks'
   `Options`, each one recorded by the task that leaves it.
4. `-Z` transfers still perform directly until the engine's multi handle lands (the phase's `CurlMulti` task).
5. The `CurlLibrary` is never cleaned up, as the runner's engine never was: the process ends with the run.

## Consequences

- The tool now goes through `curl_easy_init`, `curl_easy_setopt` (for the URL), `curl_easy_perform` and
  `curl_easy_cleanup`, so `CURLOPT_ERRORBUFFER` handling and any later `CurlEasy` behaviour apply to it.
- `CurlCommandRunnerEasyTransferTests` still counts one engine `PerformAsync` per attempt, now reached through
  `CurlEasy.PerformAsync`; the output is unchanged.
- The plan's phase 3 line "re-base `Curl.Console` onto `Libcurl`: serial transfers" is split as above; the plan
  document is corrected when its next task touches it.
