# ADR-0817: The OpenSSL build expands `--ciphers` as an OpenSSL cipher string

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3552
- Decided by Claude under Stewart's delegation.
- Amends ADR-0011's reading of `--ciphers` in the OpenSSL build.

## Context

ADR-0011 matched each `--ciphers` entry against suite names only, so `DEFAULT`, `HIGH:!aNULL`,
`aRSA` and `DEFAULT@SECLEVEL=0` selected nothing and Curl failed with exit 59 where curl's OpenSSL
build (8.18.0, OpenSSL 3.5.5, measured in WSL against `openssl s_server -tls1_2`) prints
`hello tls` and exits 0 (third-party gap report CGR-023). curl hands the value to OpenSSL's
`SSL_CTX_set_cipher_list`, so the expansion is OpenSSL's, not curl's.

## Decision

`OpenSslCipherString` expands `--ciphers` with `SSL_CTX_set_cipher_list`'s rules
(`ssl_cipher_process_rulestr`), over the TLS 1.2-and-below suites of `OpenSslCipherSuites`' table:

- The table is in the order of OpenSSL 3.5.5's `ALL` list (`openssl ciphers -s -tls1_2 ALL`);
  every alias selects its suites in that order. `DEFAULT` at the start of the value is OpenSSL's
  default list, the table without the DHE-DSS suites (`COMPLEMENTOFDEFAULT`), which is the list
  offered when `--ciphers` is absent.
- Elements are separated by `:`, `,`, `;` and spaces. A plain element adds the suites not yet
  in the list and not killed, `-` removes them, `!` removes them for good, `+` moves them to the
  end; aliases joined by `+` select their intersection. The aliases are the key exchange,
  authentication, cipher, MAC, protocol and strength names OpenSSL defines that select any suite
  of the table (`ALL`, `HIGH`, `aRSA`, `kECDHE`, `ECDHE`, `AESGCM`, `SHA1`, `TLSv1.2` and so on)
  and every OpenSSL suite name; an unknown alias selects nothing and is skipped, as OpenSSL skips it.
- `@STRENGTH` sorts the list by cipher strength, keeping the order of equals; `@SECLEVEL=0` to `5`
  is accepted and not kept, since every suite in the table passes levels 0 to 2. Any other `@`
  command, or an empty alias (`!!`), is OpenSSL's `invalid command` and fails the value: exit 59.
- An element starting `TLS_` that names a suite stays that suite, as ADR-0011 has it.
- `DHE-DSS-DES-CBC3-SHA` stays selectable by name only: OpenSSL 3.5.5 has 3DES disabled, so no
  alias selects it.

A list left empty is exit 59 with `failed setting cipher list: <value>`, as before.

## Consequences

The repro commands of CGR-023 succeed; `--tls13-ciphers` is unchanged (OpenSSL reads it as a
plain list). The suites OpenSSL has and `TlsCipherSuite` lacks (CCM, the ECDHE ARIA and CAMELLIA
suites) are not offered, as before.
