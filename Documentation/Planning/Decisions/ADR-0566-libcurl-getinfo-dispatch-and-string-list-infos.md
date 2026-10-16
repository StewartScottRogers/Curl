# ADR-0566 — `Libcurl`'s `CurlEasy.GetInfo` reads `TransferInfo` through a per-group reader dispatch; strings, lists and `TLS_SSL_PTR` answer first

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2412.

## Context

ADR-0506 and phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` give `Libcurl` a
`GetInfo(CurlInfo.X)` for every `CURLINFO_*`. The seam's `IEasyTransfer.Info` is a `TransferInfo`
record (`Curl.Api.UnitLibrary`) with one property per info, except `CURLINFO_PRIVATE`, which is
`EasyBehaviourOptions.Private`. Three tasks fill the infos by group (BL-2412 strings and lists,
BL-2413 longs, BL-2414 times, sizes and speeds), each in its own folder so lanes can run them apart,
as the option groups do (ADR-0560).

## Decision

1. `CurlInfo` (`Libcurl/Info`) is every `CURLINFO_*` of curl 8.22.0's `curl.h` with upstream's
   number (type base plus own number, `CURLINFO_EFFECTIVE_URL` 0x100001 to `CURLINFO_PROXYAUTH_USED`
   0x200046), named as `CurlOption` names `CURLOPT_*`, with `HttpCode` as the alias of `ResponseCode`.
2. `CurlEasy.GetInfo(CurlInfo info, out object? value)` is `curl_easy_getinfo`. One `out object?`
   carries every type, as `SetOption` takes one `object?`; the value's .NET type follows the info's
   base: `string?` for `_STRING`, `long` for `_LONG`, `_SOCKET` and `_OFF_T`, `double` for `_DOUBLE`,
   and for `_SLIST`/`_PTR` an `IReadOnlyList<string>` (`SSL_ENGINES`, `COOKIELIST`), a list of such
   lists (`CERTINFO`) or a `CurlTlsSessionInfo`.
3. It goes through the internal `EasyInfoDispatch`: an id `curl.h` lacks answers `UnknownOption`,
   as `curl_easy_getinfo` does; a known info no group reads yet answers `NotBuiltIn`, as an option no
   group sets does (ADR-0560); otherwise `Ok` and the reader's value. On a code other than `Ok` the
   value is `null`.
4. Groups add rows by implementing `EasyInfoReaders.AddStringReaders`, `AddLongReaders` or
   `AddTimeReaders` in a part of the class in `Libcurl/Info/<Group>`, keeping the namespace
   `Libcurl.Info`. Each reader is a named static method wrapped in an explicit `new EasyInfoReader(...)`:
   a lambda or an implicit (cached) method group adds complexity or a branch per row to the adding
   method in the coverage report, which put 18 lambdas at complexity 36.
5. `CURLINFO_TLS_SSL_PTR` and the deprecated `CURLINFO_TLS_SESSION` answer a
   `CurlTlsSessionInfo(Backend, Internals)`, upstream's `struct curl_tlssessioninfo`: `Backend` is
   `TransferInfo.TlsSslBackend` (8, Schannel, on Windows; 1, OpenSSL, elsewhere, ADR-0549) and
   `Internals` is always `null`, since Curl's TLS is managed and has no native pointer to give.
   `CURLINFO_PRIVATE` reads `EasyBehaviourOptions.Private` back.

## Consequences

- BL-2413 and BL-2414 each add one `EasyInfoReaders` part and never edit the dispatch or `CurlEasy`.
- A caller casts the `object?`; typed convenience overloads can be added later without changing the
  dispatch.
