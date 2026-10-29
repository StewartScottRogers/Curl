# ADR-0980 — Host names are mapped with the whole UTS #46 table and NFC, from generated compact tables

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3829
- Decided by Claude under Stewart's delegation.

## Context

ADR-0895 (BL-3548) mapped host names that are not ASCII by hand for a subset of UTS #46:
characters mapped to nothing, `char.ToLowerInvariant`, full-width ASCII, a few ligatures and
composition of Latin, Greek and Cyrillic letters. curl 8.22.0 with libidn2 2.3.8 maps with the
whole non-transitional IDNA mapping table of Unicode 15.1 and puts the result in NFC, so hosts
such as `ſtraſſe`, `ℌello`, `①②`, `㎏`, half-width katakana, Arabic presentation forms, `𝐀`,
Hangul jamo and reordered combining marks reached a different punycode host from curl's.
`Curl.Console` runs with invariant globalization, where `IdnMapping` and `string.Normalize` map
nothing, and only the base class library may be used.

## Decision

- `CurlUrlHostNameMapping.Map` replaces every character the IDNA mapping table maps, including
  `disallowed_STD3_mapped` (libidn2 does not apply the STD3 rules), drops ignored characters and
  the joiners (curl's transitional retry drops them), and leaves deviations (`ß`, final sigma)
  and disallowed characters as they are; validity is BL-3588's.
- It then applies NFC by hand: full canonical decomposition, canonical reordering by combining
  class, and canonical composition with the composition exclusions, Hangul by arithmetic.
- The data lives in `CurlUrlHostNameMappingTable` as three Base64 strings of unsigned LEB128
  numbers (ranges with gaps, code points as zig-zag differences), read once into dictionaries.
  That keeps the source compact and costs no reflection or resource stream, so it stays
  Native AOT compatible. The table was generated from `IdnaMappingTable.txt`, `UnicodeData.txt`
  and `CompositionExclusions.txt` (Unicode 15.1) by the C# file-based app
  `Generate-CurlUrlHostNameMappingTable.cs` at the repository root:
  `dotnet run Generate-CurlUrlHostNameMappingTable.cs -- <IdnaMappingTable.txt> <UnicodeData.txt> <CompositionExclusions.txt> [output]`
  from the repository root rewrites the table (or `output`) byte for byte (BL-4034).
- Measuring the pinned hosts against curl 8.22.0 with libidn2 on Linux is BL-4033: lanes run on
  Windows, whose curl uses WinIDN and reads an ANSI argv.

## Consequences

Every host name is mapped as UTS #46 maps it, outside the BMP included; ADR-0895's hand-written
ranges are gone. A newer Unicode version means regenerating the table with that app.
