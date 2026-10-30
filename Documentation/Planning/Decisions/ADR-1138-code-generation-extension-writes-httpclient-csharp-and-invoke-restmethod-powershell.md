# ADR-1138 — The code generation extension writes HttpClient C# with `--csharp` and Invoke-RestMethod PowerShell with `--powershell`

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4158.

## Context

`--libcurl <file>` writes a C program that makes the same transfers through libcurl. .NET and PowerShell users want
the same for their own HTTP stacks. Upstream curl has no such option, so this is an extension (ADR-0999), tested
against the HttpClient and Invoke-RestMethod documentation, never changing what curl does. It needs no new seam: a
transfer hook (ADR-1007) sees each transfer's request before running it unchanged.

## Decision

- **Names.** The extension is `code-generation` in `curl --extensions`. Two options, each taking a file name, given
  alone or together: `--csharp <file>` and `--powershell <file>`.
- **When it writes.** Before each transfer of the option group the hook records the request and writes every named
  file again, so a command line with several URLs gives one program sending them in order, as `--libcurl` writes one
  program for all its transfers. A file that cannot be written is reported on the diagnostic log as "Failed to open
  <file> to write code" and the transfer still runs, as `--libcurl` only warns.
- **Specification followed.** The .NET 10 HttpClient documentation for C#: a C# file-based program (`dotnet run
  file.cs`) with one `HttpClientHandler` (`AllowAutoRedirect` false unless `-L`, `MaxAutomaticRedirections` from
  `--max-redirs`, `AutomaticDecompression` `All` for `--compressed`, else `None`), an `HttpRequestMessage` per
  request with `new HttpMethod(...)`, fields added with `TryAddWithoutValidation` (on `Content.Headers` for the
  fields `HttpContentHeaders` holds), the body as `ByteArrayContent`, and the response copied to standard output.
  The PowerShell 7 `Invoke-RestMethod` documentation for PowerShell: `-Uri`, `-Method` for a `WebRequestMethod`
  name (exact upper case only) else `-CustomMethod`, `-Headers` (fields of one name joined with `", "`),
  `-ContentType`, `-Body` as a byte array, `-MaximumRedirection` (0 unless `-L`), and always `-SkipHeaderValidation`
  and `-SkipHttpErrorCheck`, since curl sends fields as given and does not fail on an error status.
- **What is sent.** The method curl sends (`-X`, else POST for a body, PUT for `-T`, HEAD for `-I`, else GET); the
  URL as sent (no fragment); `-A`, `-e`, the body's `Content-Type`, `--oauth2-bearer` as `Bearer` and credentials as
  `Basic` (RFC 7617), each unless `-H` gave that field; then every `-H` field (`Name;` sends it empty, `Name:` sends
  nothing, since the generated code adds no field it could remove). A body is written as UTF-8 text when it is valid
  UTF-8, else as base64, so its bytes are exact.

## Left out, and why

- A body curl streams (`-F` with a file part, `-T`): its bytes are read only while sending, so the generated code
  keeps the method but sends no body.
- `-` for standard output: the generated code would mix with the response body on the same stream.
- TLS, proxy, cookie, timeout and authentication-negotiation options: HttpClient and Invoke-RestMethod configure them
  differently enough that a partial mapping would mislead; the request itself is what is generated.

## Consequences

`Curl.Extension.CodeGeneration.UnitLibrary` holds the extension; its tests check the generated text against the
named documentation sections with no network.
