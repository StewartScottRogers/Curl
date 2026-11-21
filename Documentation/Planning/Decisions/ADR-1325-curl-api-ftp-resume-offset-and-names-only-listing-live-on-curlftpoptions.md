# ADR-1325: Curl.Api's FTP resume offset and names-only listing live on CurlFtpOptions

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5661).

## Context
`Curl.Api` had no `CURLOPT_RESUME_FROM_LARGE` or `CURLOPT_DIRLISTONLY`; recipes sent `REST` through `PreQuote`.
The engine already honours both through `EasyTransferOptions` (`-C`, `-l`).

## Decision
`CurlFtpOptions.ResumeFrom` (-1 means the end of the remote file for an upload) and `CurlFtpOptions.ListOnly`
are set on the handle's transfer options whenever an FTP record is applied. They sit with FTP, not on every
protocol: the task asked for FTP, and a general resume offset for HTTP, FILE and SFTP can be added as its own
option later without changing these.

## Consequences
A set FTP record resets the transfer's resume offset and listing mode to its own values (a request's record
replaces the client's whole, as for every other member).
