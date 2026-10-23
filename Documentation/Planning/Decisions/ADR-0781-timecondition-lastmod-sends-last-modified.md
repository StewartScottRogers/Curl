# ADR-0781 — CURL_TIMECOND_LASTMOD sends Last-Modified

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3198
- Decided by Claude under Stewart's delegation.

## Context

libcurl 8.22.0's `Curl_add_timecondition` (`lib/http.c`) names the request header by
`CURLOPT_TIMECONDITION`: `If-Modified-Since` for `CURL_TIMECOND_IFMODSINCE` (1),
`If-Unmodified-Since` for `CURL_TIMECOND_IFUNMODSINCE` (2) and `Last-Modified` for
`CURL_TIMECOND_LASTMOD` (3), each followed by the time value in RFC 1123 form in GMT and each
skipped when a custom header names it. `Curl_meets_timecondition`, which every protocol uses to
decide whether the response's document time meets the condition, has no case for
`CURL_TIMECOND_LASTMOD`: it falls into the default branch, the if-modified-since comparison. Curl
mapped value 3 to `If-Modified-Since` throughout, so its request carried the wrong header
(Stewart's report 26). The curl tool cannot set value 3 (`-z` sets only 1 or 2), so the
behaviour is read from the 8.22.0 source rather than measured.

## Decision

`Curl.Protocol.Abstractions`' `TimeCondition` gains `SentAsLastModified`. `TransferContextFactory`
maps `CURL_TIMECOND_LASTMOD` to an `IfModifiedSince` condition with it set, and
`HttpRequestHeadFormatter` names the header `Last-Modified` when it is set. No new
`TimeConditionKind` member: the kind is the comparison, which for value 3 is the
if-modified-since one, so the `file`, FTP and HTTP response checks read it unchanged, as upstream's
default branch does.

## Consequences

A handle with `CURLOPT_TIMECONDITION` 3 and a `CURLOPT_TIMEVALUE` sends `Last-Modified: <date>` and
no `If-Modified-Since`; values 1 and 2 and the curl tool's `-z` are unchanged. RTSP still sends
`If-Modified-Since` for value 3.
