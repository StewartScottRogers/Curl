# ADR-0664: curl_global_init_mem's callbacks cover each easy handle's own allocations

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation (BL-2598)

## Context

`curl_global_init_mem` replaces libcurl's `malloc`, `free`, `realloc`, `strdup` and `calloc`. Upstream
test509 (`lib509.c`) installs counting callbacks, then calls `curl_easy_init`, sets
`CURLOPT_USERAGENT` and calls `curl_easy_escape`, and prints `Callbacks were invoked!` when any was
called. Libcurl checked the five callbacks and ignored them, so test509 printed nothing. The managed
engine allocates through the .NET runtime; it has no C heap to route through the callbacks.

## Decision

A library made by `CurlGlobal.InitMemory` gives each easy handle an `EasyAllocations` over its
callbacks, which stand for the C allocations libcurl's own calls make at Libcurl's surface:

- `curl_easy_init` and `curl_easy_duphandle`: one `calloc(1, 64)`, the handle.
- `curl_easy_setopt` with a string value it accepts: one `strdup` of the string.
- `curl_easy_escape` given a handle: one `malloc` of the answer's length plus its NUL.
- `curl_easy_cleanup`: one `free` of every block above that the callback answered non-zero.

`realloc` is never called, nor is any callback for the engine's own work, and a library made by
`CurlGlobal.Init` calls none. The blocks are not used for data: the managed values stay managed.

## Consequences

- test509 passes and is on the passing list.
- A program counting calls sees libcurl's pattern (allocate at init and setopt, free at cleanup), not
  libcurl's exact counts; no upstream case pins exact counts through these callbacks.
- The handle-less `curl_escape` and `CurlStrings.Escape(ReadOnlySpan<byte>)` allocate through nothing,
  having no handle to name the library.
