namespace Curl.Extension.OAuth2.Options;

/// <summary>Says which OAuth2 option is missing or wrong; the hook fails its transfer with exit 2 and this message.</summary>
/// <param name="message">What is wrong, as a sentence.</param>
internal sealed class OAuth2OptionException(string message) : Exception(message);
