using Curl.Api.Codes;

namespace Curl.Engine;

/// <summary>Why <see cref="CallbackUploadResume" /> could not skip a read-callback upload's offset: the code and text that end the perform.</summary>
/// <param name="Code">The perform's code.</param>
/// <param name="Text">libcurl's error text.</param>
internal sealed record UploadResumeFailure(CurlCode Code, string Text);
