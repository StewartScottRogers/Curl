# ADR-0362: --libcurl writes nothing for options the Schannel build refuses

- Status: Accepted
- Date: 2026-10-02
- Decided by Claude under Stewart's delegation (BL-1174)

## Context

curl 8.21.0's mingw Schannel build refuses `--log-level`, `--log-file`, `--dns-servers`, `--dns-interface`,
`--dns-ipv4-addr`, `--dns-ipv6-addr`, `--ech`, `--ssl-sessions`, `--tlsuser`, `--tlspassword`, `--tlsauthtype`,
`--proxy-tlsuser`, `--proxy-tlspassword`, `--proxy-tlsauthtype`, `--knownhosts`, `--http2`,
`--http2-prior-knowledge`, `--http3` and `--http3-only` (exit 2, no `--libcurl` file), while Curl accepts them
because the OpenSSL builds do. Its `--proto all` list has no `smb` or `smbs`.

## Decision

`LibcurlSourceCode` writes no line for these options, and leaves `smb` and `smbs` out of
`CURLOPT_PROTOCOLS_STR` and `CURLOPT_REDIR_PROTOCOLS_STR`, as ADR-0326 already writes the Schannel build's
lines on every platform.

## Consequences

The generated program is the one the Schannel build would generate had it accepted the option. Matching the
OpenSSL builds' lines instead is a new task that revisits ADR-0326 as a whole, not option by option.
