# ADR-0843: CURLINFO_CONTENT_LENGTH_UPLOAD is the last HTTP request's body size

- **Status:** Accepted
- **Date:** 2026-10-26
- **Decided by Claude under Stewart's delegation** (BL-3656, CGR-127)

## Context

`CURLINFO_CONTENT_LENGTH_UPLOAD(_T)` came from the handle's options alone
(`ManagedEasyTransfer.ContentLengthUploadOf(EasyOptions)`): `CURLOPT_INFILESIZE` under `CURLOPT_UPLOAD`, else the
post's size, else -1. The third-party gap report (CGR-127) measured libcurl 8.22.0 (Schannel, Windows) and 8.18.0
(OpenSSL, Linux) answering differently after an HTTP request: 0 for a plain `GET`, 0 after a post a followed 301
turned into a `GET`, and -1 for a post sent chunked under a `Transfer-Encoding: chunked` header, where Curl answered
-1, 3 and 3. libcurl sets the upload size from the client reader of each request it sends
(`Curl_pgrsSetUploadSize` with the reader's total length), so the answer is the last request's body.

## Decision

Once an `http` or `https` request has been sent (`TransferReport.RequestSize` above 0) and the transfer is neither an
upload nor a form post, `ManagedEasyTransfer.ContentLengthUploadOf(EasyOptions, TransferReport?, string)` answers the
last request's body size: 0 when it had none (no post, `CURLOPT_NOBODY`, `CURLOPT_HTTPGET`, or a redirect whose
followed request is a `GET` or `HEAD`), -1 when a `Transfer-Encoding: chunked` header sends it chunked, else the
declared post size. Everything else - uploads, form bodies, other schemes, transfers that sent no request - keeps the
options' answer, which the report did not find wrong. The values are the report's measurements of real libcurl;
the libcurl probe it used is not in the repository.

## Consequences

The three CGR-127 cases answer as libcurl does. Form posts still answer -1 where libcurl would give the MIME size;
no report has measured that yet.
