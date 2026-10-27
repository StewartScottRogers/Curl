# ADR-0858: `curl_easy_perform` of a handle in a multi handle answers `CURLE_FAILED_INIT`

- **Status:** Accepted
- **Date:** 2026-10-26

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-3676 (CGR-147).

## Context

An application that adds an easy handle to a multi handle, drives it to done and leaves it attached, then
calls `curl_easy_perform` on it, got a second transfer from Curl (`rc=0`, another `GET /` on the pooled
connection). Measured on libcurl 8.22.0 (Schannel, Windows; Git for Windows' `ucrt64/bin/libcurl-4.dll`)
with a P/Invoke probe and a recording loopback server: the call answers 2 (`CURLE_FAILED_INIT`), the
`CURLOPT_ERRORBUFFER` holds `easy handle already used in multi handle`, and the server sees only the one
`GET /`. That is `easy_perform`'s `data->multi` check.

## Decision

`ManagedEasyTransfer.PerformAsync` answers `CurlCode.FailedInit` with that error text, before anything is
set up or sent, when the handle is in a multi handle (`MultiOwners`) and the multi handle did not start the
perform. The multi handle already hands each perform it starts a `MultiCallbacks` marshal just before it
starts it; the perform now takes that marshal as it starts (leaving the property empty), so a perform with
none is the application's own call. The recursive-call check of ADR-0850 still comes first, so a handle's
own callback calling `curl_easy_perform` on it keeps answering `CURLE_RECURSIVE_API_CALL`, as measured there.

## Consequences

The check sits in the engine, so `Libcurl` and every other caller of `IEasyTransfer.PerformAsync` get it.
A handle removed from its multi handle performs again as before. Engine tests that performed a handle
directly while it was in a multi handle now drive the multi handle instead.
