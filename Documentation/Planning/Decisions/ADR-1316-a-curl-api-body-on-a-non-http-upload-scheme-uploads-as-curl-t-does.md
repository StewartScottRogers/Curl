# ADR-1316: A Curl.Api body on a non-HTTP upload scheme uploads, as `curl -T` does

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5594
- Decided by Claude under Stewart's delegation.

## Context

`Curl.Api` turned every request body into `CURLOPT_POST` with post fields whatever the scheme. Over FTP that sent `RETR`, over SMTP a byte body became a literal `PUT` or `VRFY` command, s3 and az refused `-X PUT`, TFTP sent a read request, and TELNET ignored the body. `curl -T` uploads to all of these.

## Decision

A request with a `Body` whose URL scheme is ftp, ftps, sftp, scp, smb, smbs, smtp, smtps, tftp, telnet, s3 or az sets `CURLOPT_UPLOAD`, with the body as the read source (the read and seek callbacks) and its length as `CURLOPT_INFILESIZE_LARGE` (-1 when a non-seekable stream's length is unknown). Byte and stream bodies behave the same. The HTTP method is ignored on these schemes (no `CURLOPT_CUSTOMREQUEST`), as `curl -T` ignores it without `-X`; a request with no body is unchanged. A multipart body keeps its `CURLOPT_MIMEPOST` path (SMTP mail with attachments). HTTP and HTTPS are untouched: POST and PUT with a body behave as before.

## Consequences

A body is never sent as a download or a literal command on these schemes. A caller wanting a custom FTP command still sets it through the options, not the body.
