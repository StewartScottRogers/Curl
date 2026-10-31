---
id: GF-0097
title: An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts
area: environment
platform: linux
key: environment:ssl-cert-dir-empty-value-crashes
severity: Critical
status: open
scope: target
introduced-in:
opened: 2026-10-19_0804
closed:
regression: true
items: [environment:SSL_CERT_DIR, environment:SSL_CERT_FILE, environment:[url-protocol]_PROXY]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
task: BL-2822
tasks: [BL-2822]
---
# GF-0097 - An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts

## Summary

Curl differs from upstream curl in environment on linux: An empty SSL_CERT_DIR (or CA path) crashes Curl with an unhandled ArgumentException (exit 134) as soon as a TLS transfer starts.

## Evidence

environment:SSL_CERT_DIR: expected 'exit 0; stdout ''; stderr ''; request GET / HTTP/1.1', actual 'exit 134; ... Unhandled exception. System.ArgumentException: The value cannot be an empty string. (Parameter 'path') at System.ArgumentException.ThrowNullOrEmptyException ...; request none'. environment:SSL_CERT_FILE: same expected and actual. environment:[url-protocol]_PROXY: expected 'exit 35; ...; request CONNECT gap.invalid:443 HTTP/1.1', actual 'exit 134; ... same ArgumentException ...; request CONNECT gap.invalid:443 HTTP/1.1'. The CURL_CA_BUNDLE and SSLKEYLOGFILE recipes (they bypass the SSL_CERT_DIR lookup) match. Reproduce on linux: pwsh -NoProfile -File Gap/Tools/Measure-EnvironmentGap.ps1 -OutFile $env:TEMP/environment.json

## Suggestion

Most likely cause: CaCertificateEnvironment.Apply (Curl.Cli.UnitLibrary) copies the SSL_CERT_DIR value verbatim, and the measuring tool most likely clears the variable by setting it empty on Linux. The empty value reaches ServerCertificateVerification.ReadCaCertificateDirectory (Curl.Networking.UnitLibrary) and Directory.GetFiles("") throws ArgumentException, which ListFilesOrNothing does not catch. Fix: (1) in ListFilesOrNothing and ReadTextOrNothing catch ArgumentException and NotSupportedException too, so an empty or malformed --capath adds nothing, as in curl's OpenSSL build. (2) In ReadCaCertificates, treat an empty CaCertificateFile as an unusable file (exit 77) and never let an unhandled exception escape. An empty SSL_CERT_FILE would otherwise crash in File.ReadAllText(""). (3) Confirm with a probe what curl 8.22.0 does with SSL_CERT_DIR='' and SSL_CERT_FILE='' and match it (docs/cmdline-opts/_ENVIRONMENT.md, CURL_CA_BUNDLE / SSL_CERT_DIR / SSL_CERT_FILE). Add focused unit tests in Curl.Networking.UnitTests and Curl.Cli.UnitTests. This is a linux/macos cause (the Schannel build reads none of these variables).

## Measurements

- 2026-10-19_0804: 3 of 3 items are gaps.
- 2026-10-19_1329: 0 of 3 items are gaps.
- 2026-10-22_0148: 0 of 3 items are gaps.
- 2026-10-23_1637: 0 of 3 items are gaps.
- 2026-10-25_1542: 0 of 3 items are gaps.
- 2026-10-30_2103: 1 of 3 items are gaps.

## Log

- 2026-10-19_0804: Opened by gap-environment.
- 2026-10-19_0927: Filed BL-2822.
- 2026-10-19_1329: Closed: run 2026-10-19_1329 measured every item as match or excluded on linux.
- 2026-10-30_2103: Reopened as a regression: run 2026-10-30_2103 measured environment:[url-protocol]_PROXY as gap again on linux.
