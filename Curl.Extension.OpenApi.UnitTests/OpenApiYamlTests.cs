using System.Text;

namespace Curl.Extension.OpenApi;

/// <summary>Checks the YAML reader against YAML 1.2.2: the constructs OpenAPI 3.1 §4.3 descriptions use, each in its named section.</summary>
[TestClass]
public sealed class OpenApiYamlTests
{
    [TestMethod]
    [DataRow("a: 1\nb: x", """{"a":1,"b":"x"}""", DisplayName = "§8.2.2 block mapping")]
    [DataRow("a:\n  b: c\n  d: e\nf: g", """{"a":{"b":"c","d":"e"},"f":"g"}""", DisplayName = "§8.2.2 nested block mapping")]
    [DataRow("\"a b\": 1\n'c': 2\n: 3", """{"a b":1,"c":2,"":3}""", DisplayName = "§8.2.2 quoted and empty keys")]
    [DataRow("a:\nb: ~", """{"a":null,"b":null}""", DisplayName = "§8.2.2 empty values")]
    [DataRow("- a\n- b", """["a","b"]""", DisplayName = "§8.2.1 block sequence")]
    [DataRow("a:\n- 1\n- 2\nb: 3", """{"a":[1,2],"b":3}""", DisplayName = "§8.2.1 sequence at its key's indentation")]
    [DataRow("- a: 1\n  b: 2\n- c", """[{"a":1,"b":2},"c"]""", DisplayName = "§8.2.1 compact mapping in an entry")]
    [DataRow("- - a\n  - b", """[["a","b"]]""", DisplayName = "§8.2.1 compact sequence in an entry")]
    [DataRow("-\n- # c\n-\n  x: 1", """[null,null,{"x":1}]""", DisplayName = "§8.2.1 empty entries")]
    [DataRow("a: [1, \"x\", {b: c, 'd': [], e}, ]", """{"a":[1,"x",{"b":"c","d":[],"e":null}]}""", DisplayName = "§7.4 flow collections")]
    [DataRow("a: [1,\n  2] # c\nb: {x: http://h:80/p, y: }", """{"a":[1,2],"b":{"x":"http://h:80/p","y":null}}""", DisplayName = "§7.4 flow over several lines")]
    [DataRow("# c\n[a,\n b]", """["a","b"]""", DisplayName = "§7.4 flow top node")]
    [DataRow("a: one\n  two\n\n  three\nb: c", """{"a":"one two\nthree","b":"c"}""", DisplayName = "§7.3.3 multi-line plain scalar")]
    [DataRow("a: 'it''s # no comment' # comment", """{"a":"it's # no comment"}""", DisplayName = "§7.3.2 single-quoted scalar")]
    [DataRow("a: \"one\n  two\"\nb: 1", """{"a":"one two","b":1}""", DisplayName = "§7.3.1 multi-line double-quoted scalar")]
    [DataRow("a: \"\\t\\\" \\\\ \\/ \\x41\\u00e9\\U000000E9\\0\"", "{\"a\":\"\\t\\\" \\\\ / Aéé\\u0000\"}", DisplayName = "§5.7 escapes")]
    [DataRow("a: |\n  x\n   y\n\nb: 1", """{"a":"x\n y\n","b":1}""", DisplayName = "§8.1.2 literal block scalar")]
    [DataRow("a: >\n  one\n  two\n\n  three\n    indented\n  four\n", """{"a":"one two\nthree\n  indented\nfour\n"}""", DisplayName = "§8.1.3 folded block scalar")]
    [DataRow("a: |-\n  x\n\nb: >+\n  y\n\nc: |\nd: |+\n\n", """{"a":"x","b":"y\n\n","c":"","d":"\n\n"}""", DisplayName = "§8.1.1.2 chomping")]
    [DataRow("a: |2\n   x\n  y\nb: >-1\n  z", """{"a":" x\ny\n","b":" z"}""", DisplayName = "§8.1.1.1 indentation indicator")]
    [DataRow("# head\na: 1 # c\n  # indented comment\nb: x#y", """{"a":1,"b":"x#y"}""", DisplayName = "§6.6 comments")]
    [DataRow("%YAML 1.2\n---\na: 1", """{"a":1}""", DisplayName = "§6.8 directives and §9.1.3 document start")]
    [DataRow("", "null", DisplayName = "§9.1 empty document")]
    [DataRow("[null, Null, ~, true, False, TRUE, FALSE, false, True, NULL, 0, -1.5e3, 012, 1., .5, 0x1F, yes, '1']",
        """[null,null,null,true,false,true,false,false,true,null,0,-1.5e3,"012","1.",".5","0x1F","yes","1"]""", DisplayName = "§10.3.2 and §10.2.1.4 resolution")]
    public void ToJson_Construct_GivesTheSameNodeInJson(string yaml, string json) =>
        Assert.AreEqual(json, Encoding.UTF8.GetString(OpenApiYaml.ToJson(yaml)));

    [TestMethod]
    [DataRow("a: [1")]
    [DataRow("a: [1,")]
    [DataRow("a: 'x")]
    [DataRow("a: {b:")]
    [DataRow("a: {b")]
    [DataRow("a: [\"1\" 2]")]
    [DataRow("a: {b: 1 ]")]
    [DataRow("a: [\"b]")]
    [DataRow("a: \"x")]
    [DataRow("a: \"x\" y")]
    [DataRow("{a: 1} x")]
    [DataRow("a: |x")]
    [DataRow("a: \"\\q\"")]
    [DataRow("a: \"\\x4\"")]
    [DataRow("a: \"\\xZZ\"")]
    [DataRow("a: \"\\uD800\"")]
    [DataRow("a:\n  b: 1\n c: 2")]
    [DataRow("a: 1\n- b")]
    [DataRow("a: 1\nplain")]
    public void ToJson_NotYamlItReads_ThrowsFormatException(string yaml) =>
        Assert.ThrowsExactly<FormatException>(() => OpenApiYaml.ToJson(yaml));

    [TestMethod]
    [DataRow("a: &x 1")]
    [DataRow("a: *x")]
    [DataRow("- !tag x")]
    [DataRow("[&a 1]")]
    public void ToJson_AnchorAliasOrTag_ThrowsNotSupportedException(string yaml) =>
        Assert.ThrowsExactly<NotSupportedException>(() => OpenApiYaml.ToJson(yaml));
}
