using Curl.Conformance.UnitProgram.Hashes;

namespace Curl.Conformance.UnitProgram;

public sealed partial class UnitPrograms
{
    /// <summary>The ported unit programs of the Hashes area (BL-2842 to BL-2848); its port task appends here and nowhere else.</summary>
    private static IUnitProgram[] HashesPrograms =>
    [
        new Unit1302Base64(),
        new Unit1600NtHash(),
        new Unit1601Md5(),
        new Unit1610Sha256(),
        new Unit1611Md4(),
        new Unit1612HmacMd5(),
        new Unit1615Sha512Slash256(),
        new Unit1979AwsSigV4CanonicalPath(),
        new Unit1980AwsSigV4CanonicalQuery(),
    ];
}
