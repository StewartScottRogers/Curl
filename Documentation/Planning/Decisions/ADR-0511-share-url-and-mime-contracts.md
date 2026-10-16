# ADR-0511 — The share, URL and MIME contracts

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2314.

## Context

ADR-0506, ADR-0509 and ADR-0510 define the engine contracts for easy and multi handles. BL-2314
adds the share handle (`curl.h`), the URL handle (`urlapi.h`) and the MIME and formadd builders
(`curl.h`) in `Curl.Api.UnitLibrary/EngineContracts/Share`, `/Url` and `/Mime`, matching libcurl
8.22.0. A few shapes had more than one reasonable answer.

## Decision

- **Share.** `IShareHandle` has `Share`/`Unshare` of a `CurlLockData`, `SetLockCallback` and
  `SetUnlockCallback` (`ShareLockCallback`, `ShareUnlockCallback`, with `CurlLockAccess`), each
  returning `CurlShareCode` because `curl_share_setopt` can answer `CURLSHE_IN_USE`.
  `curl_share_cleanup` is a `Cleanup()` member returning `CurlShareCode`, not `IDisposable`: it
  can refuse with `CURLSHE_IN_USE`, which a dispose cannot report. `CURLSHOPT_USERDATA` is the
  callbacks' closures. `EasyBehaviourOptions.Share` (`CURLOPT_SHARE`), an `object?` until now,
  becomes `IShareHandle?`, so only a share handle can be set.
- **URL.** `ICurlUrlHandle : IDisposable` (`curl_url_cleanup` returns nothing) with
  `Get(part, flags, out value)`, `Set(part, value, flags)` returning `CurlUrlCode`, and
  `Duplicate()`. `CurlUrlPart` and the `[Flags]` `CurlUrlFlags` carry upstream's numbers.
- **MIME.** libcurl's MIME API is handle based, so it is two interfaces: `IMimeBuilder`
  (`curl_mime`: `AddPart()`, and `Build()` returning the `EasyMimeBody` data model of ADR-0508)
  and `IMimePartBuilder` (`curl_mimepart`: one `Set*` member per `curl_mime_*` setter, each
  returning `CurlCode`). The encoder stays a string, as `EasyMimePart.Encoder` is. `curl_mime_free`
  has no member: dropping the builder frees it.
- **formadd.** `ICurlFormBuilder` holds upstream's `httppost`/`last_post` pair as state:
  `Add(IReadOnlyList<CurlFormArgument>)` is one `curl_formadd` call returning `CurlFormAddCode`,
  `Serialize(FormGetCallback)` is `curl_formget`, and `Build()` gives the same `EasyMimeBody`.
  `CurlFormArgument` pairs a `CurlFormOption` with an `object?` value, since `curl_formadd` is
  variadic and each option's value has its own type. `CURLFORM_LASTENTRY` has no member, as
  `CURL_LOCK_DATA_LAST` and `CURL_LOCK_ACCESS_LAST` have none: they are counts, not values.
- **Engine.** `ICurlEngine` gains `CreateShare()`, `CreateUrl()`, `CreateMime()`, and
  `CreateForm()` for the form builder, since `curl_formadd` has no init function of its own.

## Consequences

Phase 3 implements these over Curl's `CurlUrl` parser and `Curl.Core`'s `Multipart`; `Libcurl`
maps its C handles onto them one to one.
