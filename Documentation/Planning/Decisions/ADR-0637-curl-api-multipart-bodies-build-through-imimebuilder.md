# ADR-0637 — `Curl.Api` multipart bodies build through `IMimeBuilder` at send time

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2506.

## Context

BL-2506 asks for a `CurlMultipartContent` body - text fields, files, streams, nested parts, per-part
headers and encoders - built through the seam's `IMimeBuilder` (`ICurlEngine.CreateMime()`) and
disposed with the request. ADR-0634's `CurlContent.ApplyTo` saw only the options, not the engine, so
a body could not create a MIME handle. The shape of a part, when the builder is made, and how a
failing `curl_mime_*` setter reaches the caller were open.

## Decision

- `CurlMultipartContent` is a third `CurlContent` kind: an immutable list of `CurlMimePart`s given to
  its constructor (`params IEnumerable<CurlMimePart>`). Being immutable it cannot contain itself, so no
  self-nesting check is needed; the same body may be sent again or nested in several parts.
- `CurlMimePart` is an immutable record made by one factory per content kind - `Text` (UTF-8),
  `Bytes`, `File` (`curl_mime_filedata`), `Stream` (`curl_mime_data_cb` through ADR-0634's
  `StreamUpload`, from the stream's position at send time) and `Multipart` (`curl_mime_subparts`) -
  with `Name`, `FileName`, `ContentType`, `Encoder` and `Headers` (`Name: value` lines) changed by
  `with`. A `File` part's `FileName` is set only when given, so the file's base name stays otherwise.
- `CurlContent.ApplyTo` takes the engine and a list of owned handles. A multipart body creates a new
  `IMimeBuilder` per send (and one per nested body), fills it in part order - name, content, file
  name, type, encoder, headers - and sets `CURLOPT_MIMEPOST` with `CURLOPT_POST` off and no post
  fields, as libcurl's `CURLOPT_MIMEPOST` replaces `CURLOPT_POST`. A method other than `POST` keeps
  `CURLOPT_CUSTOMREQUEST`, as for other bodies.
- The first setter that does not return `CURLE_OK` throws `CurlException` with that code and
  `CurlCodeText`'s message, before anything is performed: a file the engine cannot read is
  `CURLE_READ_ERROR`, an unknown encoder `CURLE_BAD_FUNCTION_ARGUMENT`.
- `curl_mime_free` has no seam member (dropping a builder frees it), so every builder that is
  `IDisposable` goes in the response body stream's `Owned` (ADR-0635) and is disposed after the easy
  handle when the transfer ends, whether it succeeded, failed or never started.

## Consequences

- Callers write `new CurlMultipartContent(CurlMimePart.Text("name", "value"), CurlMimePart.File("f", path))`.
- Each send rebuilds the MIME handle, so a stream part restarts from the stream's position then.
