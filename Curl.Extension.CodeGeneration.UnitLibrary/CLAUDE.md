# Curl.Extension.CodeGeneration.UnitLibrary

The `--csharp` and `--powershell` extension (ADR-0999, ADR-1128): the way `--libcurl` writes C, it writes the HTTP
requests a command line sends as C# using `HttpClient` and as a PowerShell script using `Invoke-RestMethod`.
Namespace `Curl.Extension.CodeGeneration`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the
shared layers it brings.

- `CodeGenerationExtension` gives the two options, the description and the hook activator, which
  `Curl.Extension.Composition` registers.
- `CodeGenerationTransferHook` records each transfer's request and writes every asked-for file again before the
  transfer runs unchanged.
- `GeneratedRequest` reads the method, URL, fields, body and redirect settings from a transfer.
- `CSharpProgram` and `PowerShellScript` write the two files; `CodeFile` pairs a path with its writer.

Tested against the HttpClient and Invoke-RestMethod documentation, not against curl. Held to the four quality
gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.CodeGeneration.UnitTests`.
