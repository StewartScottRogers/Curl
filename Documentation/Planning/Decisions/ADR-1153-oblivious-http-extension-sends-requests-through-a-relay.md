# ADR-1153: The Oblivious HTTP extension sends requests through a relay with --ohttp-relay and --ohttp-keys

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4137
- Decided by Claude under Stewart's delegation

## Context

BL-4311, BL-4309 and BL-4310 built HPKE base mode (RFC 9180), Binary HTTP (RFC 9292) and Oblivious HTTP key
configurations and encapsulation (RFC 9458 sections 3 and 4, ADR-1113) in `Curl.Extension.ObliviousHttp.UnitLibrary`.
BL-4137 adds the client flow and the options that turn it on. Upstream curl has no Oblivious HTTP, so the extension is
tested against RFC 9458 (January 2024) and RFC 9540 (February 2024), not against curl, and without its options a
transfer is unchanged (ADR-0999).

## Decision

- **Options.** `--ohttp-relay <url>` names the relay resource and turns the extension on. `--ohttp-keys <file|url>`
  names the gateway's key configuration: a value starting `http://` or `https://` is fetched with GET and
  `Accept: application/ohttp-keys`, anything else is read as a file holding an `application/ohttp-keys` list
  (RFC 9458 section 3.2). Without `--ohttp-keys` the keys are fetched from the target origin's
  `/.well-known/ohttp-gateway`, the gateway location RFC 9540 ("Gateway Location", "Key Configuration Fetching")
  defines for a target that is its own gateway. The description is listed as `ohttp-relay`.
- **Flow** (`ObliviousHttpTransferHook`, a transfer hook registered in `ExtensionTransferHookCatalogue.cs`, as HttpCache
  and OAuth2 are, since the extension adds no scheme and so has no line in `ExtensionCatalogue.cs`): the first
  configuration and KDF and AEAD pair the library supports is chosen; the transfer's request (method, scheme,
  authority, path and query or `--request-target`, the `-H` lines, `-A`'s User-Agent and `-d`'s form Content-Type where
  no line gives them, and the `-d` body) is encoded as a known-length Binary HTTP request, encapsulated (section 4.3)
  and POSTed through curl's own HTTP stack (`TransferContextRequests.WithRequest`, BL-4303) to the relay with
  `Content-Type: message/ohttp-req` (section 5). The `message/ohttp-res` answer is decapsulated (section 4.4) and its
  content written to the output; `-i` and `-D` get an `HTTP/1.1 <code> ` status line with an empty reason phrase
  (Binary HTTP carries none), the header fields and a blank line. `-w`'s `%{response_code}` and `%{content_type}` are
  the inner response's.
- **Exit codes**, curl's nearest: a URL that does not parse 3; a keys file that cannot be read 26; a relay or key
  fetch that is not 200 22 ("The Oblivious HTTP relay returned error: 403"); a wrong Content-Type, a malformed or
  unsupported key configuration, or a response that does not decode or authenticate 8; a failed relay or key
  transfer keeps its own result.

## Left out, and why

- **Uploads (`-T`), `-I`, `-r`, `-C`, `-z` and `-F`** fail with exit 2 ("--ohttp-relay cannot send ..."): the hook API
  carries these into the relay's POST as well as the inner request, so the relay would see them.
- **The relay and key fetches carry the transfer's own `-H`, `-A`, `-b` and `--user`**, because the hook API can replace
  the URL, method and body of a request but not curl's own header options. The hook does replace the body, so the
  `-d` data travels only inside the encapsulated request. The key fetch is a GET with an empty body. Removing the
  transfer's own headers from the outer request needs a new hook API in `Curl.Extension.Abstractions`; that is
  follow-up work.
- **Informational responses and trailers** in the Binary HTTP response are decoded but not written; `-f` applies to
  the relay's answer, not the inner response.
- **Chunked Oblivious HTTP** (the later chunked-ohttp draft) is not an RFC and is not implemented.
- **SVCB discovery** of the `ohttp` parameter (RFC 9540 section on the SvcParamKey) is not used: the user names the
  relay, and the gateway is the `--ohttp-keys` source or the target's well-known gateway.
