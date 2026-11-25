# ADR-1378: An HTTP transfer carries the mail options, for a redirect followed to a mail URL

- Status: Accepted
- Date: 2026-11-25
- Task: BL-5936
- Decided by Claude under Stewart's delegation.
- Amends ADR-0121's "`Mail` is null for every scheme but smtp(s), pop3(s), imap(s)".

## Context

curl keeps `--login-options`, `--mail-from`, `--mail-rcpt` and the other mail options on the handle, so they apply
when `-L --proto-redir all` follows an HTTP redirect to a mail URL. Measured on curl 8.22.0 (BL-5925, BL-5936): after
a 302 to `smtp://v@host` it sends `AUTH PLAIN`, `MAIL FROM`, `RCPT TO` and `DATA`; to `imap://v@host/INBOX` it sends
`AUTHENTICATE PLAIN` and selects INBOX. Curl built `MailRequestOptions` only for the first URL's mail or LDAP scheme,
so an HTTP first URL lost them all.

## Decision

- The engine also builds `MailRequestOptions` for an `http` or `https` URL, with or without `-L`: the HTTP handler
  ignores them, so building them always is simplest.
- `RedirectFollower` is unchanged: it already carries the first hop's `Mail` to every hop, withholding the
  `--oauth2-bearer` token from a hop that may not be sent credentials.

## Consequences

`ITransferContext.Mail` is no longer null for an HTTP transfer the engine builds. `-X` reaches a mail hop as its
custom command, as curl's one `CURLOPT_CUSTOMREQUEST` does.
