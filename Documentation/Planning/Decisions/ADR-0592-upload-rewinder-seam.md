# ADR-0592 — An upload that cannot seek rewinds through `IUploadRewinder`

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2463.

## Context

A resend of an upload - a followed 307 or 308, an authentication retry, a resend after a 417, a
retry after a refused HTTP/3 stream - rewound only a stream that could seek, through
`Stream.Position`. libcurl's read callback cannot claim `CanSeek`, because a dozen handlers then read
`Length - Position` for a size it does not have, yet upstream curl 8.22.0 rewinds it through
`CURLOPT_SEEKFUNCTION` in `cr_in_rewind`, and fails the transfer with exit 65
(`CURLE_SEND_FAIL_REWIND`) when the seek callback fails. BL-2417 needs a seam to hang that on.

## Decision

1. `Curl.Protocol.Abstractions` gains `IUploadRewinder`: `bool HasBeenRead` and
   `string? RewindToStart()`, which gives `null` on success or the message the transfer fails with.
   It is named apart from `Curl.Original.Protocol.Http`'s `IRewindableUpload.CanRewind`, which only says
   whether a stdin rewind would fail (ADR-0524).
2. Only an upload that cannot seek is rewound through it; a seekable stream keeps its `Position`
   path even when it implements the interface.
3. Nothing is rewound when `HasBeenRead` is false, as `cr_in_rewind` checks `read_len`: the stream
   says whether it was read, because `RedirectFollower` never sees the reads the handler makes.
4. `RedirectFollower` rewinds after it reports the hop as followed, as curl rewinds when the next
   request starts. A failure ends the chain with exit 65 and the rewinder's message, the redirect
   counted as followed and `%{redirect_url}` empty, as the multipart rewind failure already does
   (BL-402), and reports the message and `rewind of client reader 'cr-in' failed: 65` as `-v` lines.
5. In the HTTP handler a failed rewind is carried on the retry's plan
   (`HttpRequestPlan.UploadRewindFailure`, which replaces the old `UploadCannotRewind` flag and keeps
   stdin's `seek callback returned error 2`), and the retry fails before anything is sent, on a new
   connection or on the same one. `MayRetry` lets a rewinder upload answer a 401 or 407, which a
   stream that cannot seek may not (ADR-0034).

## Consequences

BL-2417 makes `CallbackReadStream` implement `IUploadRewinder` over `CURLOPT_SEEKFUNCTION`, and
nothing else changes for the command line: no stream the tool builds implements it.
