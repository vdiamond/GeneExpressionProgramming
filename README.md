# Gene Expression Programming

C# library for Gene Expression Programming (Ferreira). Individuals are linear **Karva** chromosomes: each gene is a fixed-length K-expression that decodes to an expression tree, and a linking function combines the genes on a chromosome. `gep_console` is a console host with symbolic-regression samples and a QQQ cash-session experiment.

Agent guidance: [AGENTS.md](AGENTS.md).

## Status

`Pgep` builds on its own. The console host does not build from this repository alone: `gep_console` references three **DynamicTable** projects that are not in this tree. Clone [DynamicTable](https://github.com/vdiamond/DynamicTable) next to this repo and the console project compiles. There is no test project and no CI configuration.

`gep_console/Program.Main` calls `KarvaSPX()` only. These self-contained samples are in `Program.cs` and are commented out:

- `KarvaTest1` — `x² + 2x + 1`
- `KarvaTest2` — XOR
- `KarvaTest3` — `3x² + 25x − 1`

## Target framework

Both projects set `<TargetFramework>net10.0</TargetFramework>`, with nullable reference types and implicit usings enabled. Neither project has a NuGet `PackageReference`.

- `Pgep/Pgep.csproj` — class library. No project references.
- `gep_console/gep_console.csproj` — `Exe`. References `Pgep` and the DynamicTable projects below.

Checked with the .NET SDK **10.0.404**.

## Layout

```
.gitignore
AGENTS.md                     build, dependencies, and agent limits
.cursor/rules/                coding-standards.mdc, architecture.mdc
Pgep/                         class library, namespace Pgep
  Chromosome.cs               Gene, Chromosome, ChromosomePool
  GeneticOperations.cs
  KarvaEvaluator.cs           KarvaDecoder (text form, not the evaluator)
  Parameters.cs               Parms* configuration objects
  Population.cs               Population, RegressionFitness, ITestCaseRange
  Selection.cs
  SymbolLibrary.cs
  Pgep.csproj
gep_console/
  Program.cs                  host experiments
  gep_console.csproj
  gep_console.slnx            Pgep, gep_console, and the DynamicTable projects
```

There is no solution at the repository root and no `*Tests*` project.

`gep_console/gep_console.slnx` and `gep_console/gep_console.csproj` point at these paths, which sit **outside** this repo (sibling of the repo root):

- `../../DynamicTable/Table/Table/Table.csproj`
- `../../DynamicTable/TableCsv/TableCsv/TableCsv.csproj`
- `../../DynamicTable/TableExtension/TableExtension/TableExtension.csproj`

## Build and run

Build the library. This succeeds. `Pgep/Selection.cs` reports warning **CS8600** (`Chromosome best = null`). Warnings are not treated as errors.

```bash
dotnet build Pgep/Pgep.csproj
```

Build the solution and run the host after DynamicTable is cloned next to this repo. Both commands then compile. `dotnet run` starts `KarvaSPX()` and throws `FileNotFoundException` because `D:\predict\index_extract.csv` is not in the repository.

```bash
dotnet build gep_console/gep_console.slnx
dotnet run --project gep_console/gep_console.csproj
```

Without that sibling checkout, `dotnet build gep_console/gep_console.slnx` fails with MSB3202 (the three project files are missing), and `dotnet run --project gep_console/gep_console.csproj` fails with CS0246 for `Table`, `TableCsv`, `TableExtension`, and `IDynamicTable`. The process does not reach `Main`.

`dotnet build` and `dotnet test` with no project, run from the repository root, fail with MSB1003. The root directory has no project or solution file.

`KarvaSPX` is the live entry point. It loads `D:\predict\index_extract.csv` (`Program.ExtractPath`) through DynamicTable, keeps rows whose QQQ open/high/low/close/volume changed, trains on years other than 2022, and holds out 2022. The target is the next cash-session close-to-close yield. The source says not to load `index_extract_ta.csv` and not to add a `qqq_yield_la1` variable.

## Tests

There is no test project. Without DynamicTable, `dotnet test gep_console/gep_console.slnx` fails on the missing projects. With DynamicTable present it exits 0 and still runs no suite.
