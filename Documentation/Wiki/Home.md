# Curl wiki

Cross-cutting explanations that belong to no single project, and the glossary. The wiki
lives in the repository, not in the GitHub Wiki, so it changes in the same pull request
as the code it describes.

| Page | What it covers |
| --- | --- |
| [Glossary](Glossary.md) | One term, one meaning, one name in code. |
| [Test diagnostics](Test-Diagnostics.md) | The START, END and SLOW: lines every unit test writes, and the ARRANGE, ACT, ASSERT, BYTES, DIFF and PHASE lines a test adds through `TestDiagnostics`. |
| [Adversarial testing](Adversarial-Testing.md) | The method and rules every per-project adversarial black-box test task follows: boundaries, malformed input, invalid partitions, state and concurrency. |
| [Command-line parsing](Command-Line-Parsing.md) | How `Curl.Cli.UnitLibrary` reads arguments into options or refuses them with curl's own lines and exit 2. |
