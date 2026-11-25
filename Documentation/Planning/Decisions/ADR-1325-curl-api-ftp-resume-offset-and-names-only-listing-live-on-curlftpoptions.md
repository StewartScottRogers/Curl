# ADR-1325: Curl.Api's FTP resume offset and names-only listing live on CurlFtpOptions

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5661).

## Context
`Curl.Api` had no `CURLOPT_RESUME_FROM_LARGE` or `CURLOPT_DIRLISTONLY`; recipes sent `REST` through `PreQuote`.
The engine already honours both through `EasyTransferOptions` (`-C`, `-l`).

## Decision
`CurlFtpOptions.ResumeFrom` (-1 means the end of the remote file for an upload) and `CurlFtpOptions.ListOnly`
are set on the handle's transfer options whenever an FTP record is applied. They are declared on the FTP record but are transfer-wide, as `curl -C` and `-l` are: both members apply to every
protocol that reads the shared transfer options (an HTTP request sends `Range: bytes=N-`; ListOnly also reaches SFTP and SCP).
This is amended by ADR-1328, which records the transfer-wide behaviour; the record is FTP-named because the task asked for FTP.

## Consequences
A set FTP record resets the transfer's resume offset and listing mode to its own values (a request's record
replaces the client's whole, as for every other member).
