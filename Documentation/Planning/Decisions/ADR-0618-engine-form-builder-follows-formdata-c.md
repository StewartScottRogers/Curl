# ADR-0618 — The engine's form builder follows curl 8.22.0's `lib/formdata.c`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2440.

## Context

ADR-0506 put the deprecated form API (`curl_formadd`, `curl_formget`, `curl_formfree`) behind
`Curl.Api`'s `ICurlFormBuilder`, which `Libcurl`'s `CurlForm` wraps (ADR-0586), and `ManagedCurlEngine.CreateForm`
threw `NotSupportedException`. The seam's options are typed `CurlFormArgument`s, not C varargs; `Serialize`
(`curl_formget`) is synchronous, as upstream's is; and the engine already builds a `multipart/form-data` body
from an `EasyMimeBody` with `Curl.Core`'s `MultipartFormBodyBuilder` (BL-2439).

## Decision

1. `ManagedFormBuilder` reads each call as `FormAdd` does (`FormAddCall`): one `FormFieldInfo` node per field,
   a further `CURLFORM_FILE` or `CURLFORM_CONTENTTYPE` after a file adding a node for one more file; the
   `OPTION_TWICE`, `NULL`, `ILLEGAL_ARRAY` and `UNKNOWN_OPTION` checks in upstream's order; then the completeness
   pass (`FormPost.Complete`): `INCOMPLETE` for a field with no name or value or a file with a content length,
   `NULL` for a NUL inside `CURLFORM_NAMELENGTH`, and a file or buffer with no type given the type its name ends in
   (`Curl_mime_contenttype`'s table), else the type of the node before, else `application/octet-stream`. A
   failed call adds nothing.
2. A value of the wrong .NET type counts as a missing one (`CURL_FORMADD_NULL`), since a C caller can only pass a
   null pointer there; a length that is not a `long` is 0. `CURLFORM_FILENAME` or `CURLFORM_BUFFER` with no value
   answers `NULL` where upstream would crash in `strdup`. A string value becomes UTF-8 bytes, cut at its first NUL
   unless a length is given.
3. `Build` makes the parts `Curl_getformdata` makes: a field with several files is one part named by the field
   whose `multipart/mixed` sub-parts are the files; `-` as a file is standard input; `CURLFORM_FILECONTENT` sends
   no file name; `CURLFORM_FILENAME` names a file, buffer, stream or every file of several. A `CURLFORM_STREAM`
   part has no data: the read callback belongs to the easy handle, which the builder does not know, exactly as
   `curl_formget` passes none. Sending `CURLOPT_HTTPPOST`, stream parts included, is BL-2482.
4. `Serialize` builds the body with `MultipartFormBodyBuilder` (UTF-8, percent-escaped names, the injected
   boundary; curl's random one in production) and hands the callback what `curl_formget` reads from its top
   part: `Content-Type: multipart/form-data; boundary=...`, an empty line, then the body, in pieces of at most
   8192 bytes, each filled as far as the form goes. A short take answers `CURLE_READ_ERROR` (26), as does a file
   that cannot be opened. Because `curl_formget` is synchronous it blocks on the asynchronous build, the same
   bridge `CurlEasy.Perform` uses.

## Consequences

`Libcurl`'s `CurlForm` works against the managed engine. Bytes that are not UTF-8 and stream parts cannot be sent
until BL-2470 and BL-2482. Upstream test650 is a `<tool>lib650</tool>` case, which the conformance harness does not
run yet, so the bytes are pinned by `ManagedFormBuilderTests` from the layout `lib/mime.c` gives.
