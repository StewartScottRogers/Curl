# ADR-0682 — CA certificates from `CURL_CA_BUNDLE`, `SSL_CERT_DIR` and `SSL_CERT_FILE` off Windows

- **Status:** Accepted
- **Date:** 2026-10-18

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2723.

## Context

curl 8.22.0's tool, in `transfer_per_config` (`src/tool_operate.c`), looks for CA certificates in the
environment for each option group when the group has no `--cacert`, no `--capath`, and checks a
certificate (`-k` is not given, or a `--doh-url` is given without `--doh-insecure`). Its `cacertpaths`
then reads:

1. `CURL_CA_BUNDLE`: when set, it becomes the CA file and nothing else is read;
2. otherwise `SSL_CERT_DIR` becomes the CA folder and `SSL_CERT_FILE` the CA file, each when set.

It skips the lookup when the TLS backend is Schannel: "If Schannel is the selected SSL backend then these
locations are ignored", so the CA file is only ever the one `--cacert` names (the Windows-only
`curl-ca-bundle.crt` search is skipped with it).

Measured by gap-platforms run 39108080557 (BL-2721), with a loopback TLS server whose certificate a
throwaway root issued and each variable naming that root (for `SSL_CERT_DIR`, a folder holding it as
`<subject hash>.0`): curl built with OpenSSL exits 0 for every variable on Linux and macOS, and Curl
exited 60 for `CURL_CA_BUNDLE` on Linux and for all three on macOS, because Curl read none of them (on
Linux the .NET runtime's OpenSSL read `SSL_CERT_FILE` and `SSL_CERT_DIR` itself). The Windows
measurement, against curl's Schannel build, already matched: none of the three changes the outcome.

## Decision

The CLI parser, once a command line is read, applies curl's lookup to every option group
(`Curl.Cli.UnitLibrary/CaCertificateEnvironment.cs`): same conditions, same order, values verbatim,
into `CaCertificateFile` and `CaCertificateDirectory`, where `--cacert` and `--capath` already go.
When the parser reads the command line as the Schannel build does (Windows), it reads none of the
three. `--ai-help` names the three variables in the `--cacert` and `--capath` sections.

## Consequences

- An HTTPS transfer to a server whose root only these variables name succeeds on Linux and macOS as it
  does with curl, and `--libcurl` shows the `CURLOPT_CAINFO`/`CURLOPT_CAPATH` the variables set, as curl's does.
- Windows behaviour is unchanged.
- The proxy's CA options are untouched: curl's lookup sets only the target's.
