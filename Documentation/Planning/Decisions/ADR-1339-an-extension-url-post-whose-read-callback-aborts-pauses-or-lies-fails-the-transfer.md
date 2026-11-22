# ADR-1339: An extension URL post whose read callback aborts, pauses or lies fails the transfer

- Status: Accepted
- Date: 2026-11-22
- Task: BL-5764
- Decided by Claude under Stewart's delegation.
- Amends ADR-1337.

## Context

ADR-1337 reads a `CURLOPT_READFUNCTION` body to an extension's URL up front. It stopped at any count that was not a plain positive one, so `CURL_READFUNC_ABORT` gave the handler the bytes read so far and the transfer succeeded, where libcurl ends with `CURLE_ABORTED_BY_CALLBACK`.

## Decision

The up-front read goes through `CallbackReadStream`, so it answers as the engine does everywhere else:

- `CURL_READFUNC_ABORT` ends the transfer with `CURLE_ABORTED_BY_CALLBACK` (42), "operation aborted by callback"; no handler runs.
- `CURL_READFUNC_PAUSE` ends it with `CURLE_READ_ERROR` (26), "Read callback asked for PAUSE when not supported": the body is read before the handler starts, so there is no transfer to pause, and libcurl gives that code and text when a pause cannot be honoured.
- A negative count or one larger than asked is `CURLE_READ_ERROR`, "read function returned funny value", as libcurl.
- Zero still ends the body.

`ManagedEasyTransfer.ExtensionPostReadFailedException` carries the code from `TransferSettingsOf` to the perform, which finishes with it.
