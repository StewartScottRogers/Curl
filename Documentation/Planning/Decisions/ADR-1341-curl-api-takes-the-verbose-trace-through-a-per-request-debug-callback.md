# ADR-1341: Curl.Api takes the verbose trace through a per-request debug callback

- Status: Accepted
- Date: 2026-11-22
- Task: BL-5797
- Decided by Claude under Stewart's delegation.

## Context

A `Curl.Api` caller sending mail with `MailRecipientAllowFails` gets status 250 and cannot tell which recipient the server refused with 550; `curl -v` shows each `RCPT TO` reply (Surly bug report 2026-11-22T2157, finding 3). `EasyCallbackOptions.Debug` (`CURLOPT_DEBUGFUNCTION`) exists and the engine already feeds it, but `CurlRequest` had no way to set it.

## Decision

A general callback, not a typed SMTP result: `CurlRequest.Debug` (`DebugCallback`). When set, the request is verbose (`CURLOPT_VERBOSE`) and the engine delivers the same events `curl -v` writes to standard error, typed `Text`, `HeaderIn`, `HeaderOut`, `DataIn`, `DataOut`. A refused recipient is a `HeaderIn` piece `550 ...`, an accepted one `250 ...`. It serves every protocol, so every "what did the server say" question is answered by it. The callback gets exactly what `-v` prints, so what `-v` hides never reaches it; no second redaction exists to drift. `CurlClientOptions` is unchanged. Default `null`: not verbose.

## Consequences

The `Curl.Api` README documents the callback and that its content equals `-v`'s.
