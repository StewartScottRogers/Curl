# ADR-0948: The OpenSSL build asks for an encrypted PEM key's pass phrase

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3624 (CGR-095)
- Decided by Claude under Stewart's delegation.

## Context

curl sets no pass phrase callback when `--pass` (or `--cert file:pass`) gives none, so
OpenSSL's default callback asks for an encrypted PEM key's pass phrase. Measured with curl
8.18.0 (OpenSSL 3.5.5) in WSL against `openssl s_server -WWW`, an AES-256 PKCS#8 key and
`setsid` (no controlling terminal):

- stdin at its end (`</dev/null`): stderr `Enter PEM pass phrase:\n` twice, then
  `curl: (43) unable to set private key file: 'cli_enc.key' type PEM`.
- the right pass phrase on stdin: `Enter PEM pass phrase:\n` once, and the transfer succeeds.
- a wrong one: `Enter PEM pass phrase:\n` once, then the exit 43 line.

The line break follows the answer because the typed pass phrase is not echoed. Curl
loaded no key and printed only the exit 43 line.

## Decision

`TlsClientOptions.PemPassPhrasePrompt` (`IPemPassPhrasePrompt`) asks for it.
`ClientCertificateLoader`'s OpenSSL-build path asks when no pass phrase was given, the key
type is PEM and the key file holds `BEGIN ENCRYPTED PRIVATE KEY` or a traditional
`Proc-Type: 4,ENCRYPTED` header: once, and again when the first ask reached the end of the
input, as measured. The Schannel build never reads a PEM key and never asks.

`TerminalPemPassPhrasePrompt.Instance` writes the prompt to standard error and reads standard
input: a line when it is redirected, keys without echo from a console. OpenSSL opens
`/dev/tty` first and falls back to stdin and stderr; with a terminal both reach the same
place, so the BCL's console stands in for `/dev/tty`. `TlsClientOptionsMapping` (the tool
and libcurl, whose OpenSSL build asks the same way) sets it for the origin and the HTTPS
proxy. A `null` prompt, the record's default, asks nothing, so tests and other callers never
block on the console.

A traditional encrypted PEM key is asked for, but the BCL cannot decrypt one, so even the
right answer fails exit 43 where curl loads it; that is left to BL-3980.
