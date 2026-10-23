# ADR-0773 — CURLOPT_POSTFIELDSIZE cuts the CURLOPT_POSTFIELDS body; a size past the data sends the data alone

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3190
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0 sends exactly `CURLOPT_POSTFIELDSIZE(_LARGE)` bytes of `CURLOPT_POSTFIELDS` and
declares that many in `Content-Length` (-1, the default, means the string's length); Curl sent the
whole buffer. For a size larger than the data, libcurl's C code reads past the caller's buffer -
undefined behaviour whose bytes no test can pin, and which the curl tool cannot be made to do, so
there is nothing safe to measure.

## Decision

`HttpRequestOptionsMapping` sends the first `PostFieldSize` bytes when that is not negative and
shorter than the data, and the whole data otherwise, with the `Content-Length` of what is sent.
A size past the data therefore sends the data alone (`abcdef` with size 10 sends `Content-Length: 6`
and `abcdef`), as `CURLOPT_COPYPOSTFIELDS`, RTSP and MQTT already clamp in Curl. Reading past the
buffer is not possible in managed code, padding would invent bytes, and declaring 10 while sending 6
would hang the server's read.

## Consequences

`CURLINFO_SIZE_UPLOAD_T` is the bytes sent. `CURLINFO_CONTENT_LENGTH_UPLOAD` still reports the size
set. A trailer-callback post (`TrailerPostFieldsBodyOf`) is not cut yet (BL-3190 Notes).
