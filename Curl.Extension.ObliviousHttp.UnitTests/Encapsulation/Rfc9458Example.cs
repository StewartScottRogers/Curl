namespace Curl.Extension.ObliviousHttp.Encapsulation;

/// <summary>The complete request and response of RFC 9458 Appendix A, in hexadecimal.</summary>
internal static class Rfc9458Example
{
    internal const string GatewayPrivateKey = "3c168975674b2fa8e465970b79c8dcf09f1c741626480bd4c6162fc5b6a98e1a";

    internal const string PublicKey = "31e1f05a740102115220e9af918f738674aec95f54db6e04eb705aae8e798155";

    internal const string KeyConfiguration = "010020" + PublicKey + "00080001000100010003";

    internal const string BinaryRequest = "00034745540568747470730b6578616d706c652e636f6d012f";

    internal const string EphemeralPrivateKey = "bc51d5e930bda26589890ac7032f70ad12e4ecb37abb1b65b1256c9c48999c73";

    internal const string EncapsulatedRequest =
        "010020000100014b28f881333e7c164ffc499ad9796f877f4e1051ee6d31bad1"
        + "9dec96c208b4726374e469135906992e1268c594d2a10c695d858c40a026e796"
        + "5e7d86b83dd440b2c0185204b4d63525";

    internal const string BinaryResponse = "0140c8";

    internal const string EncapsulatedResponse = "c789e7151fcba46158ca84b04464910d86f9013e404feea014e7be4a441f234f857fbd";

    internal static KeyConfigurationChoice Choice() =>
        KeyConfigurationList.SelectFirstSupported([Encapsulation.KeyConfiguration.Parse(Convert.FromHexString(KeyConfiguration))]);
}
