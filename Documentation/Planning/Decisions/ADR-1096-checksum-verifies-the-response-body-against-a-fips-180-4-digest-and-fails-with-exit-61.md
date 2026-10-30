# ADR-1096 — `--checksum` verifies the response body against a FIPS 180-4 digest and fails with exit 61

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4135.

## Context

BL-4135 adds the extension `Curl.Extension.Checksum.UnitLibrary` (ADR-0999): `--checksum <alg>:<hex>` checks a
download against an expected hash. Upstream curl has no such option, so there is no curl output to match; FIPS
180-4 (Secure Hash Standard, August 2015) defines the hashes, and the exit code is this ADR's choice. A transfer
hook's `ExecuteAsync` (ADR-1019) can see every body byte by running curl's handler with its own output stream.

## Decision

- **Names.** One option, `--checksum <alg>:<hex>`, and the extension name `checksum` in `curl --extensions` and
  `curl --ai-help extensions`. No scheme: it works for any transfer that writes a body, on any of curl's schemes.
- **Registration.** Like `--cache-dir` (ADR-1069) and `--segments` (ADR-1087), it adds no protocol handler, so
  `ExtensionCatalogue.cs` gets no line. Its option, description and hook activator are one line each in
  `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue`. With
  `CURL_EXTENSIONS=0` the option is not registered, so curl refuses it as an unknown option (exit 2).
- **Algorithms.** `sha1` (FIPS 180-4 §6.1), `sha224` (§6.3), `sha256` (§6.2), `sha384` (§6.5), `sha512` (§6.4), `sha512-224` and `sha512-256` (§6.7), named in either
  case, with exactly the digest's number of hexadecimal digits in either case. Hashing uses the base class
  library's `IncrementalHash`, except SHA-224 and SHA-512/t, whose hand-built cores BL-4312 added (constants computed from the primes as §4.2 and §5.3 define them).
- **A value it cannot read** (no colon, another algorithm, wrong length, a non-hexadecimal digit) refuses the
  transfer with exit 2 (`FailedInit`, as curl refuses an option value it cannot read) and the message
  `--checksum expects <alg>:<hex>: sha1, sha224, sha256, sha384, sha512, sha512-224 or sha512-256 and a digest of its length`, without
  contacting the server.
- **What is hashed.** The response body as curl writes it to its output (after `--compressed` decoding, and after
  the other hooks' body streams), not the headers. Each transfer of the option group is hashed on its own, so the
  option suits one URL per group.
- **A mismatch** fails an otherwise successful transfer with exit 61 (`CURLE_BAD_CONTENT_ENCODING`, "the content
  received could not be accepted", the nearest of curl's codes to a body that is not what it should be) and the
  message `checksum mismatch: expected <alg>:<hex>, got <alg>:<hex>`, digests in lower case. The body has already
  been written, as curl leaves the output of any transfer that fails part way; the digest is compared in fixed
  time. A transfer that failed on its own keeps its own result.
- **Without the option** no hook is activated, so every transfer runs exactly as curl's.

## Left out, with why

- SHA-224, SHA-512/224 and SHA-512/256 (FIPS 180-4 §6.3, §6.7) were left out at first, as the base class library has no
  primitive for them; BL-4312 added them as `sha224`, `sha512-224` and `sha512-256` over hand-built SHA-256 and
  SHA-512 cores, so nothing of FIPS 180-4's SHA-1 and SHA-2 families is left out.
- MD5 and SHA-3: not in FIPS 180-4, the specification the task names.
