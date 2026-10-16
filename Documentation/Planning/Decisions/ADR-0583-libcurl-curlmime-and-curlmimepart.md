# ADR-0583 — `Libcurl`'s `CurlMime` and `CurlMimePart` are thin wrappers, and `CURLOPT_MIMEPOST` takes the body when it is set

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2441.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_mime_init`, `_free`,
`_addpart`, `_name`, `_filename`, `_type`, `_encoder`, `_data`, `_filedata`, `_data_cb`, `_subparts`,
`_headers` and `CURLOPT_MIMEPOST`. The plan says they are "built on `Curl.Core`'s `Multipart`"; the
seam as it is puts that work behind `ICurlEngine.CreateMime`, whose `IMimeBuilder` and
`IMimePartBuilder` already carry every setter with libcurl's argument checks (BL-2439 implements them
in the engine), and `EasyHttpOptions.MimePost` holds an `EasyMimeBody` record, not a live handle.
Upstream's `CURLOPT_MIMEPOST` keeps the caller's `curl_mime *`, so parts added after the option is
set and before the transfer are sent.

## Decision

1. `Libcurl/Mime/Mime` holds `CurlMime` (`curl_mime`, from `CurlLibrary.MimeInit`) and `CurlMimePart`
   (`curl_mimepart`, from `CurlMime.AddPart`). Each part setter is one call to the engine's part
   builder and answers its `CurlCode`; the argument checks (an unknown encoder, a part nested inside
   itself, an unreadable file) are the engine's contract, not `Libcurl`'s.
2. The setters are named `SetName`, `SetFileName`, `SetType`, `SetEncoder`, `SetData`, `SetFileData`,
   `SetDataCallback`, `SetSubParts` and `SetHeaders`, as the seam names them. `SetData` also takes a
   `string`, sent as its UTF-8 bytes (`CURL_ZERO_TERMINATED`). `SetHeaders` copies the list, as every
   list-valued setter does (ADR-0578); `take_ownership` and `curl_mime_data_cb`'s `freefunc` have no
   counterpart.
3. `CurlMime.Dispose` is `curl_mime_free`. Afterwards the handle, its parts, a `SetSubParts` naming it
   and a `CURLOPT_MIMEPOST` given it throw `ObjectDisposedException`, where upstream's use after free
   is undefined.
4. `CURLOPT_MIMEPOST` takes a `CurlMime` or `null`, any other value `BadFunctionArgument`, and stores
   the handle's body as it is when the option is set: the `EasyOptions` record holds a value, not a
   handle. A caller that builds the MIME before setting it, as upstream's examples and curl's tool do,
   sends the same request; one that adds parts afterwards must set the option again.
5. The row is added by a new `AddMimePostSetters` partial method of `EasyOptionSetters`, implemented in
   `Libcurl/Mime/Mime`; `AddFormPostSetters` is declared beside it for `CURLOPT_HTTPPOST`
   (`Libcurl/Mime/Form`, BL-2442), so that task does not edit the shared file.

## Consequences

- `Libcurl`'s MIME area is tested with a fake `IMimeBuilder`, never the network.
- The managed engine's `CreateMime` still throws `NotSupportedException` until BL-2439.
