# AGENTS.md

## What this repo is

Gene Expression Programming (Ferreira) in C#. Individuals are fixed-length chromosomes in Karva notation; each gene decodes to an expression tree, and genes are combined by a linking function.

`Pgep` is the engine: a `net10.0` class library with no NuGet packages and no file or network I/O. `gep_console` is the host. `Main` calls `KarvaSPX()`, which loads a QQQ cash-session extract from `D:\predict\index_extract.csv` and evolves a next-session yield model. `KarvaTest1` (`x² + 2x + 1`), `KarvaTest2` (XOR), and `KarvaTest3` (`3x² + 25x − 1`) are in `gep_console/Program.cs` and are commented out.

Status: source for the library and the experiment host. It is not published as a NuGet package. There is no CI configuration and no test project. `Pgep` builds on its own. The console build needs a sibling checkout of DynamicTable, and the checked-in entry point needs a CSV that is not in this repository.

## Build, run, and test

Projects target `net10.0`. The commands below were run with .NET SDK 10.0.404.

Build the library (no other repo required):

```bash
dotnet build Pgep/Pgep.csproj
```

That succeeds. It reports one warning: `CS8600` in `Pgep/Selection.cs` (`Tournament` assigns `Chromosome best = null`).

There is no solution at the repository root. These two commands fail with `MSB1003` because the working directory has no project or solution file:

```bash
dotnet build
dotnet test
```

Build the host after DynamicTable is cloned next to this repo (see below):

```bash
dotnet build gep_console/gep_console.csproj
dotnet build gep_console/gep_console.slnx
```

Both succeed when `../DynamicTable` contains `Table`, `TableCsv`, and `TableExtension`. A fresh `gep_console.csproj` build also reports nullable warnings from those DynamicTable projects and `CS8625` in `gep_console/Program.cs` (the `null` prefix passed to `LoadCsv`). Without the sibling checkout, `gep_console.csproj` fails (`MSB9008` plus `CS0246` for `Table`, `TableCsv`, `TableExtension`, and `IDynamicTable`), and `gep_console.slnx` fails with `MSB3202` because those three project files are missing.

Run the host:

```bash
dotnet run --project gep_console/gep_console.csproj
```

With DynamicTable present, the process starts `KarvaSPX()` and throws `FileNotFoundException` for `D:\predict\index_extract.csv`. That extract is not in the repository. On Linux the `D:` path is not a drive letter, so the runtime looks for it under the working directory.

There are no tests. No test project exists. Do not add one unless Victor asks.

`dotnet test Pgep/Pgep.csproj` exits 0 after a restore and build and does not run a suite. With DynamicTable present, `dotnet test gep_console/gep_console.slnx` also exits 0 and does not run a suite. Without DynamicTable, that solution test fails with the same `MSB3202` missing-project errors as the solution build. A zero exit from these commands is not a test result.

## Layout

```
GeneExpressionProgramming/
├── Pgep/                         Class library (namespace Pgep)
│   ├── Chromosome.cs            Gene, Chromosome, ChromosomePool
│   ├── GeneticOperations.cs     In-place evolutionary operators
│   ├── KarvaEvaluator.cs        KarvaDecoder (display only; not the evaluator)
│   ├── Parameters.cs            Parms* configuration objects
│   ├── Population.cs            Population, fitness, test-case ranges
│   ├── Selection.cs             Tournament and RouletteWheel
│   ├── SymbolLibrary.cs         Symbols, builder, predefined libraries
│   └── Pgep.csproj
├── gep_console/                  Console host (global namespace)
│   ├── Program.cs               Experiments; Main calls KarvaSPX
│   ├── gep_console.csproj
│   └── gep_console.slnx         Pgep plus the three DynamicTable projects
├── .cursor/rules/
│   ├── coding-standards.mdc
│   └── architecture.mdc
├── AGENTS.md
├── README.md
└── .gitignore
```

## Dependencies on other vdiamond repos

**DynamicTable** — https://github.com/vdiamond/DynamicTable

`gep_console` references three projects as sibling-folder `ProjectReference`s. This is not a git submodule, a NuGet package, or an HTTP API.

| Project | Reference from `gep_console/` |
|---------|-------------------------------|
| `Table` | `../../DynamicTable/Table/Table/Table.csproj` |
| `TableCsv` | `../../DynamicTable/TableCsv/TableCsv/TableCsv.csproj` |
| `TableExtension` | `../../DynamicTable/TableExtension/TableExtension/TableExtension.csproj` |

`gep_console/gep_console.slnx` lists the same three paths. The host uses namespaces `Table`, `TableCsv`, and `TableExtension` to load CSV and read typed columns.

Clone it next to this repo:

```
parent/
├── GeneExpressionProgramming/     this repo
└── DynamicTable/                  git clone https://github.com/vdiamond/DynamicTable
    ├── Table/Table/Table.csproj
    ├── TableCsv/TableCsv/TableCsv.csproj
    └── TableExtension/TableExtension/TableExtension.csproj
```

`Pgep` does not reference DynamicTable and must stay that way. No downstream dependents of this repository are recorded.

## Conventions

`.cursor/rules/coding-standards.mdc` is the governing C# standard (`alwaysApply: true`). `.cursor/rules/architecture.mdc` is the governing design for genome layout, symbol contracts, pooling, evaluation, fitness, and operators. Read it before changing those areas.

Existing code may not yet comply with the coding standard. Bring code into compliance when you touch it. Do not rewrite untouched files to chase the standard.

What the code does today:

- Both projects are `net10.0` with nullable reference types and implicit usings enabled. Neither has a `PackageReference`. There is no `LangVersion` override, no root `.editorconfig`, no `Directory.Build.props`, and no CI. Warnings are not treated as errors.
- `Parms*` types are mutable classes: a public parameterless constructor, then public settable properties with defaults. They are not records. Keep that shape unless Victor asks for a change.
- Several types share a file. `Parameters.cs` holds every `Parms*` type. `Chromosome.cs` holds `Gene`, `Chromosome`, and `ChromosomePool`. `Population.cs` holds `Population`, `IFitnessFunction`, `RegressionFitness`, `ITestCaseRange`, and `TestCaseRange`. `SymbolLibrary.cs` holds the symbol types and the builder.
- Library types use namespace `Pgep`. `Program`, `TestFitness`, and `IndexListRange` are in the global namespace in `gep_console/Program.cs`.
- Interfaces use an `I` prefix. Private fields are `_camelCase` and usually sit in `#region private` after the public members. `SymbolLibraryBuilder` and `SymbolLibrary` also use `#region internal`. Properties are often `get { return _field; }` blocks.
- Most blocks use braces. Some guards do not: `if (headLength < 1) throw ...` in `Gene` and `Chromosome`, and early `return` statements in `GeneticOperations`.
- `GeneticOperations` and `Selection` are static classes. Operators write into the live `int[][]` and `double[][]` gene storage.
- Fitness is higher-is-better. `RegressionFitness` returns `1 / (1 + MSE)`, or `0` when the range is empty or the score is not finite.
- There is no `ILogger` and no dependency-injection container. `Population.Run` and the host print with `Console.WriteLine`. Evaluation is synchronous (`Parallel.ForEach` for fitness). `Pgep` uses the BCL only (`ArrayPool`, `Parallel.ForEach`, `Random`). The host may depend on `Pgep` and DynamicTable.
- XML documentation is on `KarvaDecoder` only. `KarvaDecoder` lives in `KarvaEvaluator.cs` and only formats text. Runtime evaluation is `Gene.Evaluate` / `Chromosome.Evaluate`.

## What not to touch

- Do not commit secrets, the local extract CSV, or build outputs. `.gitignore` already excludes `bin/`, `obj/`, and user/IDE files. Do not commit a copy of `D:\predict\index_extract.csv`.
- Do not change `Pgep` public behavior without asking Victor. That includes the head/tail formula (`tail = head * (maxArity - 1) + 1`), symbol order (functions, then variables, then constants), the `ISymbol.Evaluate` calling convention, chromosome linking, per-gene constant arrays, pool `Get`/`Return` and pool shape, tail-only terminals, `HeaderFreeze`, and higher-is-better fitness. `Gene.Evaluate` and `KarvaDecoder` must keep the same open reading frame.
- Do not add NuGet packages, file I/O, or network I/O to `Pgep`, and do not add a DynamicTable reference to `Pgep`. Do not retarget off `net10.0`, and do not add `.editorconfig`, `Directory.Build.props`, CI, or a service container, unless Victor asks.
- Do not merge the `Parms*` types into one options object, and do not turn them into records unless Victor asks. Do not add a second genome encoding beside Karva. Do not call `Selection.RouletteWheel` from `Evolve` (it returns a uniform random chromosome).
- In `KarvaSPXLoad`, do not load `index_extract_ta.csv` and do not `AddVariable` `qqq_yield_la1`. The next cash-session yield is the target, not a feature.
- Known gaps are listed in `.cursor/rules/architecture.mdc`. Leave them unless the task asks: no test project, `Population.Run` writes to `Console`, `Selection.RouletteWheel` is uniform random, `KarvaDecoder` lives in `KarvaEvaluator.cs`, several experiments live in one `Program.cs`, and the host does not call `Population.Release()`.

Development happens only through Victor and Cursor agents.
