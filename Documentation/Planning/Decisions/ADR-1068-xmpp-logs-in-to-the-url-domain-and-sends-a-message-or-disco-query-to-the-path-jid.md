# ADR-1068: XMPP logs in to the URL's domain and sends a message or disco query to the path's JID

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4123)

## Context

BL-4123 adds XMPP as an extension (ADR-0999), tested against RFC 6120 (core), RFC 6121 (messages), RFC 5122 (the
`xmpp:` URI) and XEP-0030 (service discovery), not against curl, which has no XMPP.

## Decision

- Schemes `xmpp` (port 5222, RFC 6120 3.2.1) and `xmpps` (port 5223, direct TLS, XEP-0368), in RFC 5122's authority
  form: `xmpp://[user@]domain[:port]/[target-jid][?query]`. The library is `Curl.Extension.Protocol.Xmpp.UnitLibrary`; it adds
  no command-line option. The URL's domain is the stream's `to`; the URL-decoded path is the target JID, or the domain
  itself when the path is empty.
- Query (RFC 5122 2.2 and the XMPP URI query registry): `?message;subject=..;type=..;body=..` sends a `<message/>`
  (its body from `-d` when given); `?disco;request=info|items;node=..` sends a disco `<iq type='get'/>`. With no query
  type, `-d` makes a message and no body a disco#info query. Any other type or request ends with exit 3 before connecting.
- Exchange (RFC 6120): stream open (4.2); STARTTLS when offered and the connection is not already TLS (5.4), through
  curl's TLS provider so `-k` and `--cacert` apply; SASL PLAIN with `--user`, ANONYMOUS without (6.4); a stream restart
  after each (4.3.3); a server-chosen resource bound (7.6); the stanza; for disco, the answer's payload written to the
  output on one line with a trailing LF (platform-neutral, no indentation); then `</stream:stream>` and reading until
  the server closes (4.4). Stanzas arriving meanwhile are skipped unless they are errors.
- Elements are read one at a time with no read-ahead past an element, since the server sends nothing after
  `<proceed/>` or `<success/>` until the client acts. DTDs are refused (11.1).
- Exit codes: a SASL failure or no usable mechanism is 67; a stream error, stanza error, unexpected element or
  malformed XML is 8; the connection ending where an element was due, or a failed read, is 56; a failed send is 55;
  a failed output write is 23; a character XML 1.0 forbids in the stanza is 43; a TLS failure keeps the provider's code.

## Left out, and why

- SCRAM-SHA-1 and SCRAM-SHA-256 (RFC 5802, 7677): not yet; PLAIN over STARTTLS works with common servers. Filed as a
  follow-up task, not left out for good.
- DNS SRV lookup of `_xmpp-client._tcp` (RFC 6120 3.2.1): the extension connects to the URL's host and port, as curl
  does for every scheme; a user names the server host directly.
- The `xmpp:` form without `//` and RFC 5122's other query types (roster, subscribe, join): curl's URL parser needs
  `scheme://`, and these change account state rather than send or query.
