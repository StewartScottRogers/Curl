namespace Curl.Extension.PrivacyPass.Tokens;

/// <summary>Pins the VOPRF(P-384, SHA-384) client against the RFC 9497 Appendix A.4.2 vectors.</summary>
[TestClass]
public sealed class VoprfP384Sha384Tests
{
    private const string Blind = "504650f53df8f16f6861633388936ea23338fa65ec36e0290022b48eb562889d89dbfa691d1cde91517fa222ed7ad364";
    private const string ProofRandomScalar = "803d955f0e073a04aa5d92b3fb739f56f9db001266677f62c095021db018cd8cbb55941d4073698ce45c405d1348b7b1";

    [TestMethod]
    [DataRow(
        "00",
        "02d338c05cbecb82de13d6700f09cb61190543a7b7e2c6cd4fca56887e564ea82653b27fdad383995ea6d02cf26d0e24d9",
        "02a7bba589b3e8672aa19e8fd258de2e6aae20101c8d761246de97a6b5ee9cf105febce4327a326255a3c604f63f600ef6",
        "bfc6cf3859127f5fe25548859856d6b7fa1c7459f0ba5712a806fc091a3000c42d8ba34ff45f32a52e40533efd2a03bc87f3bf4f9f58028297ccb9ccb18ae7182bcd1ef239df77e3be65ef147f3acf8bc9cbfc5524b702263414f043e3b7ca2e",
        "3333230886b562ffb8329a8be08fea8025755372817ec969d114d1203d026b4a622beab60220bf19078bca35a529b35c")]
    [DataRow(
        "5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a5a",
        "02f27469e059886f221be5f2cca03d2bdc61e55221721c3b3e56fc012e36d31ae5f8dc058109591556a6dbd3a8c69c433b",
        "03f16f903947035400e96b7f531a38d4a07ac89a80f89d86a1bf089c525a92c7f4733729ca30c56ce78b1ab4f7d92db8b4",
        "d005d6daaad7571414c1e0c75f7e57f2113ca9f4604e84bc90f9be52da896fff3bee496dcde2a578ae9df315032585f801fb21c6080ac05672b291e575a40295b306d967717b28e08fcc8ad1cab47845d16af73b3e643ddcc191208e71c64630",
        "b91c70ea3d4d62ba922eb8a7d03809a441e1c3c7af915cbc2226f485213e895942cd0f8580e6d99f82221e66c40d274f")]
    public void BlindFinalize_Rfc9497Vector_GivesItsBlindedElementAndOutput(string input, string blindedElement, string evaluationElement, string proof, string output)
    {
        var inputBytes = Convert.FromHexString(input);
        var voprf = new VoprfP384Sha384(_ => Convert.FromHexString(Blind));

        var blinded = voprf.Blind(inputBytes);
        var finalized = voprf.Finalize(inputBytes, VoprfTestIssuer.PublicKey, Convert.FromHexString(evaluationElement), Convert.FromHexString(proof));

        CollectionAssert.AreEqual(Convert.FromHexString(blindedElement), blinded);
        Assert.IsNotNull(finalized);
        CollectionAssert.AreEqual(Convert.FromHexString(output), finalized);
    }

    [TestMethod]
    public void Evaluate_Rfc9497VectorKeyAndProofScalar_GivesTheVectorsEvaluationAndProof()
    {
        var blinded = P384Group.Decode(Convert.FromHexString("02d338c05cbecb82de13d6700f09cb61190543a7b7e2c6cd4fca56887e564ea82653b27fdad383995ea6d02cf26d0e24d9"))!;

        var (evaluated, proof) = VoprfTestIssuer.Evaluate([blinded], VoprfTestIssuer.Scalar(ProofRandomScalar));

        CollectionAssert.AreEqual(Convert.FromHexString("02a7bba589b3e8672aa19e8fd258de2e6aae20101c8d761246de97a6b5ee9cf105febce4327a326255a3c604f63f600ef6"), evaluated);
        CollectionAssert.AreEqual(
            Convert.FromHexString("bfc6cf3859127f5fe25548859856d6b7fa1c7459f0ba5712a806fc091a3000c42d8ba34ff45f32a52e40533efd2a03bc87f3bf4f9f58028297ccb9ccb18ae7182bcd1ef239df77e3be65ef147f3acf8bc9cbfc5524b702263414f043e3b7ca2e"),
            proof);
    }

    [TestMethod]
    public void VerifyProof_Rfc9497BatchOfTwoVector_Verifies()
    {
        P384Point[] blinded =
        [
            Element("02d338c05cbecb82de13d6700f09cb61190543a7b7e2c6cd4fca56887e564ea82653b27fdad383995ea6d02cf26d0e24d9"),
            Element("02fa02470d7f151018b41e82223c32fad824de6ad4b5ce9f8e9f98083c9a726de9a1fc39d7a0cb6f4f188dd9cea01474cd"),
        ];
        P384Point[] evaluated =
        [
            Element("02a7bba589b3e8672aa19e8fd258de2e6aae20101c8d761246de97a6b5ee9cf105febce4327a326255a3c604f63f600ef6"),
            Element("028e9e115625ff4c2f07bf87ce3fd73fc77994a7a0c1df03d2a630a3d845930e2e63a165b114d98fe34e61b68d23c0b50a"),
        ];
        var proof = Convert.FromHexString(
            "6d8dcbd2fc95550a02211fb78afd013933f307d21e7d855b0b1ed0af78076d8137ad8b0a1bfa05676d325249c1dbb9a52bd81b1c2b7b0efc77cf7b278e1c947f6283f1d4c513053fc0ad19e026fb0c30654b53d9cea4b87b037271b5d2e2d0ea");

        Assert.IsTrue(VoprfP384Sha384.VerifyProof(P384Group.Generator, VoprfTestIssuer.PublicKey, blinded, evaluated, proof));
        Assert.IsFalse(VoprfP384Sha384.VerifyProof(P384Group.Generator, VoprfTestIssuer.PublicKey, [.. blinded.Reverse()], evaluated, proof));
    }

    [TestMethod]
    public void Finalize_ProofOrElementNotValid_IsNull()
    {
        byte[] input = [0x00];
        var voprf = new VoprfP384Sha384(_ => Convert.FromHexString(Blind));
        var blinded = P384Group.Decode(voprf.Blind(input))!;
        var (evaluated, proof) = VoprfTestIssuer.Evaluate([blinded], 7);
        var corrupted = (byte[])proof.Clone();
        corrupted[^1] ^= 1;

        Assert.IsNotNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated, proof));
        Assert.IsNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated, corrupted));
        Assert.IsNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated[..^1], proof));
        Assert.IsNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated, proof[..^1]));
        Assert.IsNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated, [.. Enumerable.Repeat((byte)0xFF, P384Group.ScalarLength), .. proof[P384Group.ScalarLength..]]));
        // c = s = 0 makes t2 the identity, which has no encoding.
        Assert.IsNull(voprf.Finalize(input, VoprfTestIssuer.PublicKey, evaluated, new byte[VoprfP384Sha384.ProofLength]));
    }

    [TestMethod]
    public void Blind_RandomOctetsZeroOrNotBelowTheOrder_DrawsAgain()
    {
        var draws = new Queue<byte[]>([new byte[P384Group.ScalarLength], [.. Enumerable.Repeat((byte)0xFF, P384Group.ScalarLength)], Convert.FromHexString(Blind)]);
        var voprf = new VoprfP384Sha384(_ => draws.Dequeue());

        var blinded = voprf.Blind([0x00]);

        Assert.IsEmpty(draws);
        CollectionAssert.AreEqual(Convert.FromHexString("02d338c05cbecb82de13d6700f09cb61190543a7b7e2c6cd4fca56887e564ea82653b27fdad383995ea6d02cf26d0e24d9"), blinded);
    }

    [TestMethod]
    public void Finalize_NullInput_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new VoprfP384Sha384(_ => [1]).Finalize(null!, VoprfTestIssuer.PublicKey, [], []));

    private static P384Point Element(string hex) => P384Group.Decode(Convert.FromHexString(hex))!;
}
