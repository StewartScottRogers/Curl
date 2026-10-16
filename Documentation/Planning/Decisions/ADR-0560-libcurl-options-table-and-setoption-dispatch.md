# ADR-0560 — `Libcurl`'s options table is generated from curl 8.22.0's `easyoptions.c`, and `SetOption` dispatches through setter rows each option group adds in a `partial` part

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2399.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` give `Libcurl` every
`CURLOPT_*` by name, `curl_easy_option_by_name/_by_id/_next`, and one table `SetOption` dispatches
through. Eight option-group tasks (BL-2400 to BL-2407) and the callback tasks each add their
options' setters, each from its own folder `Libcurl/Options/<Group>`, in parallel lanes. `CurlEasy`
itself (BL-2408) is a later task, so this task cannot hang `SetOption` on a handle yet.

Upstream's table, `lib/easyoptions.c` at `curl-8_22_0`, has 327 rows: 312 options and 15 alias names
flagged `CURLOT_FLAG_ALIAS`. Its `lookup` matches a name with `curl_strequal` (ASCII letters only,
ignoring case, alias rows included) and an id skipping alias rows; `curl_easy_option_next(NULL)` is
the first row and the last row's next is NULL.

## Decision

1. `CurlOption` has one member per row, numbered from `include/curl/curl.h` (`CURLOPTTYPE_*` base
   plus the option's number); an alias member has its target's value, as upstream's `#define` does.
   Member names drop `CURLOPT_` and capitalise each underscore-separated word (`ACCEPTTIMEOUT_MS` is
   `AccepttimeoutMs`), the rule `CurlCode` already follows for `CURLE_*`; hand-picked word breaks
   would be 327 judgment calls nobody can check mechanically.
2. `CurlOptions.All/ByName/ById/Next` over a `CurlEasyOption` record per row, in upstream's order.
   `ByName` upper-cases ASCII letters only and looks up ordinally, so `"fıle"` does not find
   `FILE` as `StringComparer.OrdinalIgnoreCase` would. `Next` of a row not in the table is `null`.
3. `CurlOptionType` is `CURLOT_*` with upstream's numbers. The .NET values each type accepts:
   `LONG` and `OFF_T` a `long` or `int`; `VALUES` those or any `Enum`; `STRING` a `string`; `SLIST` an
   `IEnumerable<string>`; `BLOB` a `byte[]` or `ReadOnlyMemory<byte>`; `FUNCTION` a `Delegate`;
   `OBJECT` and `CBPTR` anything but a `Delegate` (a delegate there is the `*DATA`/`*FUNCTION` mix-up
   a C compiler would warn about). Every pointer type also takes `null`; the integer types do not. A
   `bool` is not a `long`: libcurl takes `0L`/`1L`, and accepting `bool` would be a .NET convenience
   that belongs to `Curl.Api`, not the libcurl-faithful surface.
4. The internal `EasyOptionDispatch.SetOption(ref EasyOptions, CurlOption, object?)` answers
   `UnknownOption` for a number no row has, `BadFunctionArgument` for a value of the wrong type,
   `NotBuiltIn` for an option no setter row covers yet, else the setter's own answer. A setter is
   `EasyOptionSetter(ref EasyOptions, object?)`: `EasyOptions` is an immutable record, so the setter
   replaces the handle's copy. `CurlEasy.SetOption` (BL-2408) calls `EasyOptionDispatch.Default`.
5. `EasyOptionSetters.Create` calls one implementation-optional `static partial void Add<Group>Setters`
   per group (Behaviour, Transfer, Network, Tls, Authentication, Proxy, Http, Protocol, Callback). A
   group task implements its method in a part of `EasyOptionSetters` in its own folder, keeping the
   namespace `Libcurl.Options` (the same exception ADR-0556 makes for `CurlLibrary`'s parts), so no
   two lanes edit one file and an unimplemented group compiles away to nothing.

## Consequences

- The option count is pinned by a test (327); moving the targeted curl version regenerates the
  enum and table from the new `easyoptions.c` and `curl.h` and moves the pin.
- Tests reach `NotBuiltIn` through a dispatch built over an empty setter dictionary, so they stay
  true as group tasks add rows to `Default`.
