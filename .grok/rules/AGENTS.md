# AGENTS.md

Repo-specific instructions. Genome, pooling, and operator contracts are in `.grok/rules/architecture.md`. C# style is in `.grok/rules/coding-standards.md`.

## What this repo is

Gene Expression Programming library (`Pgep`, namespace `Pgep`) with Karva chromosomes, plus the console host `gep_console`. Both target `net10.0` (`Nullable` and `ImplicitUsings` enabled). `Pgep` has no package references and no project references. The host references `Pgep` and three sibling DynamicTable projects that are not in this repository. `Main` runs `KarvaSPX()`. `KarvaTest1`, `KarvaTest2`, and `KarvaTest3` are commented out.

## Build and run

```bash
dotnet build Pgep/Pgep.csproj
```

That command succeeds. Warning CS8600 in `Pgep/Selection.cs` (`Chromosome best = null`). Warnings are not errors. There is no CI.

```bash
dotnet build gep_console/gep_console.slnx
dotnet run --project gep_console/gep_console.csproj
```

Both fail in this checkout. The solution and `gep_console.csproj` reference:

- `../../DynamicTable/Table/Table/Table.csproj`
- `../../DynamicTable/TableCsv/TableCsv/TableCsv.csproj`
- `../../DynamicTable/TableExtension/TableExtension/TableExtension.csproj`

Those files are absent, so the host never starts. Do not satisfy the references by moving CSV or table code into `Pgep`.

`dotnet build` and `dotnet test` at the repository root fail with MSB1003. There is no solution file there.

## Tests

There is no test project. Do not add one unless asked. Do not run `dotnet test MyProject.Tests/` or a coverage collector. Nothing is configured.

## Conventions in this code

- Several types share a file. `Parameters.cs` holds every `Parms*` type. `Chromosome.cs` holds `Gene`, `Chromosome`, and `ChromosomePool`. `Population.cs` holds `Population`, `IFitnessFunction`, `RegressionFitness`, `ITestCaseRange`, and `TestCaseRange`. `SymbolLibrary.cs` holds the symbol types and the builder.
- `Parms*` types are mutable classes: a public parameterless constructor, then public settable properties with defaults. They are not records.
- Private fields are `_camelCase` and usually sit in `#region private` after the public members. Some builders use `#region internal`.
- Interfaces use an `I` prefix (`ISymbol`, `IFitnessFunction`, `ITestCaseRange`).
- Properties are often written as `get { return _field; }` blocks.
- Most blocks use braces. A few guards do not (`if (headLength < 1) throw ...` on `Gene`, early `return` in `GeneticOperations`).
- `GeneticOperations` and `Selection` are static classes. Operators write into the live `int[][]` and `double[][]` gene storage.
- Fitness is higher-is-better. `RegressionFitness` returns `1 / (1 + MSE)`, or `0` when the range is empty or the score is not finite.
- `Population.Run` prints with `Console.WriteLine`. There is no `ILogger` and no dependency-injection container.
- Evaluation is `Gene.Evaluate` / `Chromosome.Evaluate`. `KarvaDecoder` only formats text, and the type lives in `KarvaEvaluator.cs`.
- `Program`, `TestFitness`, and `IndexListRange` are in the global namespace in `gep_console/Program.cs`.
- XML comments exist on `KarvaDecoder` and are otherwise rare.

## What not to touch

- Head/tail layout (`tail = head * (maxArity - 1) + 1`), terminal-only tails, symbol order (functions, then variables, then constants), and the `ISymbol.Evaluate` argument convention. `Gene.Evaluate` and `KarvaDecoder` must keep the same open reading frame.
- Per-gene constant arrays and the fixed shape of `ChromosomePool`. Do not share one constant array across genes.
- Do not add NuGet packages. Do not give `Pgep` a reference to DynamicTable, CSV, or other file I/O.
- Do not merge the `Parms*` types into one options object, and do not turn them into records unless a refactor is requested.
- Do not call `Selection.RouletteWheel` from `Evolve`. It returns a uniform random chromosome, not a fitness-proportional pick.
- Do not add a second genome encoding beside Karva.
- Leave these alone unless asked: console output inside `Population.Run`, the `KarvaEvaluator.cs` file name, `RouletteWheel`, and the missing `Population.Release()` call in the host.
- In `KarvaSPXLoad`, do not load `index_extract_ta.csv` and do not `AddVariable` `qqq_yield_la1`. The next cash-session yield is the target, not a feature.
