# ADR-0630 — `Curl.Api` keeps its own `curl_easy_strerror` table, and `CurlException` prefers the error buffer

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2498.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` gives `Curl.Api` one exception type,
`CurlException`, carrying the `CurlCode` and its text. The text for a code lives in
`Libcurl/Misc/ErrorText/CurlEasyErrorText.cs`, but `Curl.Api` may reference only
`Curl.Api.UnitLibrary` (ADR-0506), and the seam offers only `CurlCodeConversion` (a number cast) and
`IEasyTransfer.ErrorText` (the `CURLOPT_ERRORBUFFER` content, empty when the engine gave no detail).

## Decision

- `Curl.Api/Errors/CurlCodeText.cs` holds its own copy of `curl_easy_strerror`'s text for every
  `CurlCode`, read from curl 8.22.0's `libcurl-4.dll` (mingw, Schannel) on 2026-10-16; every
  `CURLE_OBSOLETE*` number and every number outside the enum gives `Unknown error`, as libcurl does.
- `CurlException.ThrowIfFailed(code, errorText)` is the one function that maps a failed call to the
  exception: `CurlCode.Ok` returns, any other code throws with the handle's error text as the message
  when it is not empty, and the `strerror` text otherwise - what curl's tool prints in the same two cases.
- `new CurlException(CurlCode.Ok, ...)` throws `ArgumentOutOfRangeException`: success is never an exception.

## Why

Moving the table into `Curl.Api.UnitLibrary` would put text into the seam and change a library other
lanes' tasks touch, and referencing `Libcurl` breaks ADR-0506's layering. A second copy of 102 strings
is cheap, and `CurlCodeTextTests` pins it to the measured 8.22.0 output, so a drift shows as a failing test.
