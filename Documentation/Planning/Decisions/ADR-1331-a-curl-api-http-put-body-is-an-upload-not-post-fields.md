# ADR-1331: A Curl.Api HTTP PUT body is an upload, not post fields (BL-5683)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

A `Curl.Api` `PUT` with a `Body` sent the body as `CURLOPT_POSTFIELDS` under `CURLOPT_CUSTOMREQUEST` `PUT`, so libcurl
added `Content-Type: application/x-www-form-urlencoded`. `curl -T file` sends no content type. BL-5594 kept PUT
"exactly as before"; this changes it on purpose.

## Decision

- An HTTP or HTTPS `PUT` with a body that is not multipart is sent as an upload (`CURLOPT_UPLOAD` with
  `CURLOPT_INFILESIZE_LARGE`, the read and seek callbacks, no custom method), as `-T` does: no form `Content-Type`.
  A caller's own `Content-Type` header is sent as given. `CurlContent` carries no type of its own today, so none
  is added; a future content that names a type sets it as a header.
- A multipart `PUT` keeps `CURLOPT_MIMEPOST` under `CURLOPT_CUSTOMREQUEST` `PUT`, its own `multipart/form-data` type.
- `POST` keeps the form default, as `curl -d` does.

## Consequences

`UploadSchemes.UploadsWithBody` is unchanged; `CurlRequestOptions` also routes an HTTP PUT body to the upload path.
