namespace Curl.Extension.Abstractions.Description;

/// <summary>
/// What one extension tells a user about itself (ADR-1008): <c>curl --extensions-help</c> prints it as one
/// line and <c>curl --ai-help extensions</c> as a Markdown section. An extension declares it in one file and
/// registers it with one line in <c>Curl.Extension.Composition</c>'s <c>ExtensionDescriptionCatalogue</c>.
/// </summary>
/// <param name="Name">The extension's name, such as <c>sse</c>.</param>
/// <param name="Summary">One sentence saying what the extension adds.</param>
/// <param name="Schemes">The URL schemes it handles, without <c>://</c>; empty when it adds none.</param>
/// <param name="Options">The long command-line options it adds; empty when it adds none.</param>
/// <param name="Specification">The specification it is tested against.</param>
public sealed record ExtensionDescription(
    string Name,
    string Summary,
    IReadOnlyList<string> Schemes,
    IReadOnlyList<ExtensionOption> Options,
    Uri Specification);
