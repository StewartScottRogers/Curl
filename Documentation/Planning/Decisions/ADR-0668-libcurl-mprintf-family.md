# ADR-0668: Libcurl's curl_m*printf family reads each argument as its C conversion's type

- Status: Accepted
- Date: 2026-10-17
- Decided by Claude under Stewart's delegation
- Task: BL-2616

## Context

Upstream case 557 (`tests/libtest/lib557.c`) exercises all of curl 8.22.0's `lib/mprintf.c` through
`curl_mprintf`, `curl_mfprintf`, `curl_msprintf`, `curl_msnprintf`, `curl_maprintf` and their `v` forms.
BL-2612 ports lib557 to C#, so Libcurl needs these functions. C passes varargs whose type the format names;
C# has only `params object?[]`, so the C semantics have to be carried by how each argument is read.

## Decision

`Libcurl.Misc.Printf.CurlPrintf` has `MPrintf`, `MFPrintf(Stream)`, `MSPrintf(Span<byte>)`,
`MSNPrintf(Span<byte>)` (the span's length is `maxlength`) and `MAPrintf`, ported line for line from
`lib/mprintf.c`, `parsefmt` refusals, 128-argument and 128-segment limits, `out_number`, `out_string`,
`out_pointer` and `out_double`'s width and precision clamps included.

- **Arguments are read as the conversion's C type**, never as their .NET type: any .NET integer or `char` for an
  integer conversion, keeping the bits C's `va_arg` would (`%d` 32, `%ld` 32 on Windows and 64 elsewhere, as
  the platform's libcurl, `%lld`/`%zd`/`%Od` 64); `double` or `float` for a floating conversion; `string` (its
  UTF-8 bytes), `byte[]` (to its NUL) or `null` for `%s`; an integer or `null` for `%p`; a `StrongBox` of
  `long`, `int` or `short` for `%n`. Too few arguments or a type the conversion cannot take, undefined in C,
  throws `ArgumentException`. `I`, `I32` and `I64` are length modifiers on Windows only, as in libcurl.
- **The `v` forms are the same methods**: `params object?[]` already takes an argument array.
- **Bytes, not characters**: the format is UTF-8 up to its first NUL, counts are bytes, `MAPrintf` decodes
  UTF-8 and answers `null` at 8,000,000 bytes (`DYN_APRINTF`).
- **`out_double`'s `snprintf` is hand-written** over each double's exact decimal value (`ExactDecimal`, a
  `BigInteger` expansion), rounding half to even as glibc and the UCRT do, so `%f`, `%e` and `%g` answer the
  same bytes on every platform. Infinity and NaN print as glibc does (`inf`, `-nan`; .NET's `double.NaN`
  has its sign bit set), on Windows too, where the UCRT would print `-nan(ind)`.
- `MSPrintf` throws `ArgumentException` when the span cannot hold the text and its NUL, where C writes past the
  buffer.

## Consequences

lib557 can be ported over Libcurl (BL-2612). Formatting is the platform's libcurl's except for NaN on Windows.
