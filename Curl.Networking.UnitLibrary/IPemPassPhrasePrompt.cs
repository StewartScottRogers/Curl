namespace Curl.Networking;

/// <summary>
/// Asks the user for the pass phrase of an encrypted PEM private key given without
/// <c>--pass</c>, as OpenSSL's default pass phrase callback does in curl's OpenSSL build
/// (ADR-0948, CGR-095).
/// </summary>
public interface IPemPassPhrasePrompt
{
    /// <summary>
    /// Writes the prompt and reads one line.
    /// </summary>
    /// <returns>The line read, or <see langword="null" /> at the end of the input.</returns>
    string? AskPassPhrase();
}
