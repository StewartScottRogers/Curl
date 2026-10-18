namespace Curl.Conformance.LibTest.UnitTests;

[TestClass]
public sealed class LibTestProgramsTests
{
    [TestMethod]
    public void Find_RegisteredProgram_IsThatProgram()
    {
        NamedProgram lib500 = new("lib500");
        LibTestPrograms programs = new([lib500, new NamedProgram("lib501")]);

        ILibTest? found = programs.Find("lib500");

        Assert.AreSame(lib500, found);
        CollectionAssert.AreEquivalent(new[] { "lib500", "lib501" }, programs.ProgramNames.ToArray());
    }

    [TestMethod]
    public void Find_ProgramNotPorted_IsNull()
    {
        LibTestPrograms programs = new([new NamedProgram("lib500")]);

        ILibTest? found = programs.Find("lib502");

        Assert.IsNull(found);
    }

    [TestMethod]
    public void Constructor_TwoProgramsOfOneName_Throws()
    {
        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new LibTestPrograms([new NamedProgram("lib500"), new NamedProgram("lib500")]));

        StringAssert.StartsWith(exception.Message, "Two programs are named lib500.");
    }

    [TestMethod]
    public void Constructor_NullList_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestPrograms(null!));
    }

    [TestMethod]
    public void Constructor_NullProgram_Throws()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => new LibTestPrograms([null!]));

        Assert.AreEqual("programs", exception.ParamName);
    }

    [TestMethod]
    public void Ported_HoldsLib500ToLib521Lib523ToLib526Lib530Lib533Lib536ToLib544Lib547Lib549Lib552ToLib560Lib562Lib564Lib566ToLib576Lib578Lib579Lib582Lib583Lib589ToLib591Lib597ToLib599Lib643Lib650ToLib655Lib658Lib659Lib661Lib666ToLib668Lib670Lib674Lib676ToLib678Lib694Lib695Lib751Lib753Lib757Lib758Lib766Lib1156Lib1301Lib1308Lib1396Lib1398Lib1485Lib1500ToLib1502Lib1506ToLib1515Lib1517Lib1518Lib1520Lib1522Lib1523Lib1525ToLib1538Lib1540ToLib1542Lib1545Lib1549ToLib1559Lib1564Lib1565Lib1567ToLib1569Lib1571Lib1576Lib1582Lib1587ToLib1589Lib1591ToLib1594Lib1597ToLib1599Lib1647ToLib1649Lib1662Lib1678Lib1686Lib1900ToLib1903Lib1905Lib1906()
    {
        string[] names = [.. LibTestPrograms.Ported.ProgramNames.Order(StringComparer.Ordinal)];

        CollectionAssert.AreEqual(new[] { "lib1156", "lib1301", "lib1308", "lib1396", "lib1398", "lib1485", "lib1500", "lib1501", "lib1502", "lib1506", "lib1507", "lib1508", "lib1509", "lib1510", "lib1511", "lib1512", "lib1513", "lib1514", "lib1515", "lib1517", "lib1518", "lib1520", "lib1522", "lib1523", "lib1525", "lib1526", "lib1527", "lib1528", "lib1529", "lib1530", "lib1531", "lib1532", "lib1533", "lib1534", "lib1535", "lib1536", "lib1537", "lib1538", "lib1540", "lib1541", "lib1542", "lib1545", "lib1549", "lib1550", "lib1551", "lib1552", "lib1553", "lib1554", "lib1555", "lib1556", "lib1557", "lib1558", "lib1559", "lib1564", "lib1565", "lib1567", "lib1568", "lib1569", "lib1571", "lib1576", "lib1582", "lib1587", "lib1588", "lib1589", "lib1591", "lib1592", "lib1593", "lib1594", "lib1597", "lib1598", "lib1599", "lib1647", "lib1648", "lib1649", "lib1662", "lib1678", "lib1686", "lib1900", "lib1901", "lib1902", "lib1903", "lib1905", "lib1906", "lib1907", "lib1908", "lib1910", "lib1911", "lib1912", "lib1913", "lib1915", "lib1916", "lib1918", "lib1919", "lib1920", "lib1921", "lib1922", "lib1933", "lib1934", "lib1935", "lib1936", "lib1937", "lib1938", "lib1939", "lib1940", "lib1945", "lib1947", "lib1948", "lib1955", "lib1956", "lib1957", "lib1958", "lib1959", "lib1960", "lib1964", "lib1965", "lib1967", "lib1970", "lib1971", "lib1972", "lib1973", "lib1974", "lib1975", "lib1977", "lib1978", "lib1985", "lib2023", "lib2032", "lib2082", "lib2118", "lib2301", "lib2302", "lib2304", "lib2306", "lib2308", "lib2309", "lib2397", "lib2402", "lib2404", "lib2405", "lib2412", "lib2414", "lib2502", "lib2504", "lib2505", "lib2506", "lib2700", "lib3010", "lib3025", "lib3026", "lib3027", "lib3033", "lib3034", "lib3100", "lib3101", "lib3102", "lib3103", "lib3104", "lib3105", "lib3207", "lib3208", "lib500", "lib5000", "lib5004", "lib501", "lib502", "lib503", "lib504", "lib505", "lib506", "lib507", "lib508", "lib509", "lib510", "lib511", "lib512", "lib513", "lib514", "lib515", "lib516", "lib517", "lib518", "lib519", "lib520", "lib521", "lib523", "lib524", "lib525", "lib526", "lib530", "lib533", "lib536", "lib537", "lib539", "lib540", "lib541", "lib542", "lib543", "lib544", "lib547", "lib549", "lib552", "lib553", "lib554", "lib555", "lib556", "lib557", "lib558", "lib559", "lib560", "lib562", "lib564", "lib566", "lib567", "lib568", "lib569", "lib570", "lib571", "lib572", "lib573", "lib574", "lib575", "lib576", "lib578", "lib579", "lib582", "lib583", "lib589", "lib590", "lib591", "lib597", "lib598", "lib599", "lib643", "lib650", "lib651", "lib652", "lib653", "lib654", "lib655", "lib658", "lib659", "lib661", "lib666", "lib667", "lib668", "lib670", "lib674", "lib676", "lib677", "lib678", "lib694", "lib695", "lib751", "lib753", "lib757", "lib758", "lib766" }, names);
    }

    private sealed class NamedProgram(string programName) : ILibTest
    {
        public string ProgramName => programName;

        public Task<LibTestResult> RunAsync(LibTestContext context, CancellationToken cancellationToken) =>
            Task.FromResult(LibTestResult.Success);
    }
}
