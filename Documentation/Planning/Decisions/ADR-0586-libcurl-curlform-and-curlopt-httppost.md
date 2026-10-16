# ADR-0586 — `Libcurl`'s `CurlForm` is the deprecated form API over the engine's form builder, marked `[Obsolete]`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2442.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_formadd`, `curl_formget`,
`curl_formfree` and `CURLOPT_HTTPPOST`, all deprecated upstream since curl 7.56.0 in favour of the
MIME API. The seam as it is puts the work behind `ICurlEngine.CreateForm`, whose `ICurlFormBuilder`
takes one field's `CurlFormArgument` list per `Add` (with every argument check, answering a
`CurlFormAddCode`), serialises the form for `curl_formget`, and builds the `EasyMimeBody` that
`EasyHttpOptions.HttpPost` holds. Upstream's `curl_formadd(&httppost, &last_post, ...)` starts a form
when `httppost` is `NULL` and leaves it `NULL` when that first call fails.

## Decision

1. `Libcurl/Mime/Form` holds `CurlForm`, one handle standing for upstream's `httppost` and
   `last_post` pointers, marked `[Obsolete]` with a message pointing at `CurlLibrary.MimeInit`, as
   upstream marks the functions `CURL_DEPRECATED(7.56.0, ...)`.
2. `CurlForm.Add(CurlLibrary library, ref CurlForm? form, params CurlFormArgument[] arguments)` is
   `curl_formadd`: a `null` form starts one over the library's new engine form builder, and `form` is
   set only when the engine answers `Ok`. Every `CURLFORM_*` argument goes to the engine as given;
   `CURLFORM_END` is implied. The argument checks are the engine's contract, not `Libcurl`'s.
3. `Get(FormGetCallback)` is `curl_formget` and answers the engine's result. `Free(CurlForm?)` and
   `Dispose` are `curl_formfree`; `Free(null)` does nothing, and afterwards `Add` and `Get` on the
   form, and `CURLOPT_HTTPPOST` given it, throw `ObjectDisposedException`.
4. `CURLOPT_HTTPPOST` takes a `CurlForm` or `null`, any other value `BadFunctionArgument`, and stores
   the form's body as it is when set, as `CURLOPT_MIMEPOST` does (ADR-0583). Its row is
   `EasyOptionSetters`' `AddFormPostSetters`, implemented in `Libcurl/Mime/Form`.
5. `CurlOption.Httppost` itself is not marked `[Obsolete]`: the options table is generated from
   `easyoptions.c`, which lists `HTTPPOST` without a deprecation flag.

## Consequences

- Callers of the form API get a CS0618 warning naming the MIME API, as C callers get upstream's
  deprecation warning.
- The area is tested with a fake `ICurlFormBuilder`, never the network.
