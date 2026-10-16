# ADR-0565 — `Libcurl`'s HTTP option setters check values as curl 8.22.0's `setopt.c` does, `POSTFIELDS` keeps the caller's bytes and `COPYPOSTFIELDS` copies them

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2406.

## Context

BL-2406 adds a setter row (ADR-0560) for every `CURLOPT_*` that `EasyHttpOptions` holds, apart from
`MIMEPOST` and `HTTPPOST`, which the MIME tasks own. The record's shape is fixed by
`Curl.Api.UnitLibrary`. Some of libcurl's rules have no exact counterpart there: a C `char *` body
that .NET sees as an `object`, upstream's one `method` field that the record splits into `Post` and
`HttpGet`, and range checks that upstream answers in different ways.

## Decision

1. The rows live in `Libcurl/Options/Http`. `EasyOptionSetters.AddHttpSetters` calls
   `EasyHttpOptionSetters.AddTo`. Each option gets a named static method, and its row is
   `new EasyOptionSetter(Method)`. The coverage-based complexity measure counts every lambda (and
   every implicit method-group conversion, which the compiler caches) as a branch in the method that
   builds it. So 38 lambda rows put `AddTo` at complexity 74; explicit delegate creation keeps it at 1.
2. `POSTFIELDS` keeps a `byte[]` by reference (`new ReadOnlyMemory<byte>(array)`), so a later change
   by the caller is what gets sent, as in C. `COPYPOSTFIELDS` copies the bytes: the first
   `POSTFIELDSIZE` of them when that is set (never more than the buffer holds, since .NET cannot read
   past it), or else all of them. Each also accepts a `ReadOnlyMemory<byte>`, or a `string` sent as its
   UTF-8 bytes. `null` clears the body. Any other object is `BadFunctionArgument`. Both set `Post` and
   clear `HttpGet`, as upstream sets `method = HTTPREQ_POST`.
3. Upstream keeps one method field; the record's `Post` and `HttpGet` stand in for it. `POST` 1 sets
   `Post`, clears `HttpGet` and clears `Transfer.NoBody`, and `POST` 0 clears `Post`. `HTTPGET` 1 sets
   `HttpGet` and clears `Post`, `NoBody` and `Upload`. `HTTPGET` 0 changes nothing.
4. Range checks follow `setopt.c`. These answer `BadFunctionArgument`: `FOLLOWLOCATION` outside 0..3,
   `MAXREDIRS` and `POSTFIELDSIZE(_LARGE)` below -1, a negative `POSTREDIR`, a negative
   `EXPECT_100_TIMEOUT_MS`, and a negative `HTTP_VERSION`. An unknown `HTTP_VERSION` answers
   `UnsupportedProtocol`; every defined one is accepted, since Curl builds HTTP/2 and HTTP/3 in.
   `STREAM_WEIGHT` outside 1..256 is ignored with `Ok`. `EXPECT_100_TIMEOUT_MS` is cut to
   `int.MaxValue` milliseconds. `POSTREDIR` keeps only its `CURL_REDIR_POST_*` bits and
   `MIME_OPTIONS` only `FORMESCAPE`. `HSTS_CTRL` without `CURLHSTS_ENABLE` becomes `None`.
5. `COOKIEFILE` adds each file to the list, and `null` clears the list. `COOKIELIST` adds each line,
   and `null` is ignored. `HTTPHEADER` and `HTTP200ALIASES` store a copy of the given lines, and
   `null` empties them. `STREAM_DEPENDS(_E)` takes any object as the parent, because the easy handle
   type does not exist yet (BL-2408), and `null` clears it.

## Consequences

- Upstream drops an earlier `COPYPOSTFIELDS` copy when `POSTFIELDSIZE` later grows past it. This
  setter cannot tell a copy from a caller's buffer, so it does not do that.
- The engine reads `PostFields` without regard to `HttpGet`. `HTTPGET` after `POSTFIELDS` therefore
  sends a GET only once the engine honours `HttpGet`.
