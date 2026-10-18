# ADR-0651: CURLOPT_HTTPPOST is sent, its CURLFORM_STREAM parts read through CURLOPT_READFUNCTION

- Status: Accepted
- Date: 2026-10-16
- Decided by Claude under Stewart's delegation
- Task: BL-2482

## Context

BL-2440 (ADR-0618) built `ManagedFormBuilder`, whose `Build` gives the `EasyMimeBody` curl 8.22.0's
`Curl_getformdata` makes, and `Libcurl`'s `CURLOPT_HTTPPOST` setter stores it on
`EasyHttpOptions.HttpPost`. Nothing in the engine sent it, and a `CURLFORM_STREAM` part had no data,
because upstream reads it through the easy handle's read function (`data->state.fread_func`, handed to
`Curl_getformdata` by `http.c`) with the stream object as the user pointer, which `curl_formadd` does not
know. `Curl.Api.UnitLibrary`'s `EasyMimeData` has no case for "read through the handle's callback".

## Decision

1. `FormPost` gives a `CURLFORM_STREAM` part an engine-internal `FormStreamData` (`EasyMimeData`) holding
   its `CURLFORM_CONTENTSLENGTH` and stream object. `Curl.Api` is unchanged; `curl_formget`
   (`ManagedFormBuilder.Serialize`) still sends the part empty, as upstream's has no read function.
2. A perform from the options sends `EasyHttpOptions.MimePost` when it is set, else `HttpPost`
   (`MimePostForm.BodyOf`), built and posted exactly as `CURLOPT_MIMEPOST` is (ADR-0627). Each
   `FormStreamData` part becomes an `EasyMimeCallback` over `EasyCallbackOptions.Read` sized by its
   contents length, 0 being -1 (unknown, sent chunked) as `Curl_getformdata` does.
3. The stream object is not passed to the callback: a managed `ReadCallback` takes no user pointer and
   carries its data in its closure (ADR-0573), as `CURLOPT_READDATA` does for the upload. With no read
   callback set the part is sent empty; upstream would call `fread` on the stream object as a `FILE *`,
   which has no managed equivalent worth building for a deprecated API.
4. `HttpPost` is not sent to a mail URL (`smtp`, `smtps`, `imap`, `imaps`): upstream reads
   `CURLOPT_HTTPPOST` only in `http.c`.

## Consequences

`CURLOPT_HTTPPOST` forms reach the wire byte-identical to the tool's `-F` request for the same fields.
When both `MIMEPOST` and `HTTPPOST` are set, `MIMEPOST` wins whatever the order; upstream's last setter
wins (`httpreq`), which the options records cannot tell apart. Tests: `ManagedEasyTransferTests.HttpPost.cs`,
`MimePostFormTests` and `ManagedFormBuilderTests`.
