# ADR-0949: Traditional encrypted PEM keys are decrypted by hand

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3980

Decided by Claude under Stewart's delegation.

## Context

curl's OpenSSL build loads a `--key` in OpenSSL's traditional encrypted PEM form, a
`-----BEGIN RSA PRIVATE KEY-----` (or `EC PRIVATE KEY`) block with the headers
`Proc-Type: 4,ENCRYPTED` and `DEK-Info: <cipher>,<IV hex>`, when `--pass` (or the pass phrase
typed at ADR-0948's prompt) decrypts it. The BCL's `CreateFromEncryptedPem` reads only PKCS#8's
`ENCRYPTED PRIVATE KEY`, so Curl failed such a key with exit 43 even with the right pass phrase.

Measured with curl 8.18.0 on OpenSSL 3.5.5 (WSL) against `openssl s_server -WWW`, with
`cli_trad_enc.key` written by `openssl rsa -aes256 -traditional -passout pass:secret`:
`--pass secret` prints the page and exits 0 with nothing on stderr; `--pass wrong` exits 43 with
`curl: (43) unable to set private key file: 'cli_trad_enc.key' type PEM`.

## Decision

`TraditionalPemKeyDecryptor` decrypts the first encrypted block by hand: the cipher key is
OpenSSL's `EVP_BytesToKey` of the pass phrase (MD5, one iteration, the IV's first eight bytes as
salt), the body is decrypted in CBC mode with PKCS#7 padding, and the result is handed to
`X509Certificate2.CreateFromPem` as an unencrypted block under the same label.
`ClientCertificateLoader.CombineWithKey` takes that path when a pass phrase is given and the
key holds `Proc-Type: 4,ENCRYPTED`. A wrong pass phrase, an unsupported cipher or a block that
does not parse throws `CryptographicException`, which stays exit 43 with curl's text.

The ciphers are AES-128-CBC, AES-192-CBC, AES-256-CBC and DES-EDE3-CBC, the ones OpenSSL 3
writes and reads with its default provider. DES-CBC is not read: OpenSSL 3.5.5's
`openssl rsa -des` cannot even write it without the legacy provider, so the OpenSSL build curl
links fails it too.

## Consequences

Only `System.Security.Cryptography` is used (MD5, `Aes`, `TripleDES`); no package. The
hand-built key readers (Ed25519, Ed448, ML-DSA) are untouched: those keys have no traditional form.
