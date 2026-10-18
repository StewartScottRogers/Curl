# ADR-0619 — The chunk, fnmatch and interleave callbacks adapt the FTP and RTSP hooks

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2420.

## Context

`Curl.Api`'s `EasyCallbackOptions` carries `CURLOPT_CHUNK_BGN_FUNCTION` (`ChunkBeginCallback`),
`CURLOPT_CHUNK_END_FUNCTION` (`ChunkEndCallback`), `CURLOPT_FNMATCH_FUNCTION` (`FileNameMatchCallback`) and
`CURLOPT_INTERLEAVEFUNCTION` (`InterleaveCallback`), and `EasyBehaviourOptions.WildcardMatch` is
`CURLOPT_WILDCARDMATCH`. Nothing called the four, and `Libcurl` answered `CURLE_NOT_BUILT_IN` for them and their
`*DATA` options. BL-2478 gave the FTP handler `IFtpWildcardHooks` (ADR-0615) and BL-2477 gave the RTSP handler
`IRtspInterleaveSink` (ADR-0612), each with libcurl's default behaviour when none is given.

In curl 8.22.0 the chunk and fnmatch callbacks run only under `CURLOPT_WILDCARDMATCH`; each one not set falls back to
libcurl's own (`Curl_fnmatch`, download every match). `wc_statemach` treats a chunk-begin answer of
`CURL_CHUNK_BGN_FUNC_SKIP` or `_FAIL` specially and downloads on any other. `ftplistparser.c` never fills
`curl_fileinfo`'s `time`, `uid` or `gid`. The interleave callback is used for every RTSP transfer, whatever
`WILDCARDMATCH` says.

## Decision

1. **Adapters in the engine.** `CallbackWildcardHooks` implements `IFtpWildcardHooks` over the three callbacks, each
   one not set answering as `FtpDownloadEveryMatch.Instance`. `CallbackWildcardHooks.Of(options)` gives `null`
   without `WildcardMatch`, so a wildcard URL is fetched as a plain path, as libcurl does. The match and chunk-end
   answers map by number (the enums share upstream's numbers); the chunk-begin answer maps `Skip` and `Fail` and
   downloads on anything else. Each listing entry reaches the chunk-begin callback as a `CurlFileInfo`
   (`FileInfoOf`), with `Time`, `UserId` and `GroupId` left `null`.
2. **`CallbackInterleaveSink`** implements `IRtspInterleaveSink` by calling the interleave callback with the whole
   frame; `Of(null)` is `null`, so the handler keeps writing frames to the body output.
3. **Wiring.** `EngineComposition.CreateProtocolHandlers` takes `ftpWildcardHooks` and `rtspInterleaveSink`, and
   `ManagedCurlEngine.CreateDispatcher` passes the two adapters built from the transfer's options. `Curl.Console`
   passes neither: the curl tool has no wildcard or interleave option.
4. **Libcurl.** `Callbacks/Wildcard` sets the four function options (their delegate type or `null`, anything else
   `BadFunctionArgument`); `CHUNK_DATA`, `FNMATCH_DATA` and `INTERLEAVEDATA` take any value and change nothing, the
   managed callbacks carrying their user data in their closures, as the other callback areas do.

## Consequences

A libcurl caller's FTP wildcard download and RTSP interleaved RTP data run through its own callbacks.
`FileNameMatchResult.Fail` leaves an entry out rather than failing the transfer, as curl 8.22.0 does (ADR-0615).
