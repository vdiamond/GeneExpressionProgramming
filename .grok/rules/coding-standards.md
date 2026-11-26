# C# / .NET Coding Standards

**Purpose**  
This document defines the coding standards for our .NET codebase. All code — whether written by humans or AI agents — must follow these rules to produce production-quality, robust, performant, and maintainable software.

**Scope**  
Applies to new code in this repository. Do not reformat an existing file only to add braces, regions, or XML comments.

**For AI Agents**  
When writing, reviewing, or refactoring C#/.NET code, you **must** follow every rule in this document. Prioritize correctness, robustness, readability, and long-term maintainability.

---

## 1. Target Framework & Tooling

- Both projects target **`net10.0`** (`Pgep/Pgep.csproj` and `gep_console/gep_console.csproj`). Do not retarget them.
- There is no `LangVersion` override. The compiler language version is the SDK default for `net10.0`.
- Nullable reference types and implicit usings are already enabled in both projects.
- There is no `.editorconfig`, no `Directory.Build.props`, and no CI workflow. Do not add them unless asked.
- Warnings are not treated as errors. `dotnet build Pgep/Pgep.csproj` succeeds with CS8600 in `Pgep/Selection.cs`.

---

## 2. General Design Principles

- **Robustness first**: Code must handle invalid input, nulls, and edge cases defensively.
- **Immutability where the code already uses it.** `Parms*` types, genes, and chromosomes are mutable by design. Do not convert `Parms*` classes to records or init-only types unless a refactor is requested (`architecture.md`).
- **Composition over inheritance**.
- **Fail fast** with clear, meaningful exceptions or result types.
- Keep methods small and focused.
- Avoid magic strings and numbers.
- **No NuGet packages** unless the user asks. Neither project has a `PackageReference`. `Pgep` stays on the BCL. `gep_console` already references `Pgep` and the sibling DynamicTable projects; do not add a DynamicTable reference to `Pgep`. There are no Microsoft.Extensions packages in this repo.

---

## 3. Naming Conventions

Examples below are types and members in this repository. `KarvaDecoder` lives in `KarvaEvaluator.cs`; do not rename that file unless asked. Several library files hold more than one type (`Parameters.cs`, `Chromosome.cs`, `Population.cs`, `SymbolLibrary.cs`).

| Element              | Convention                          | Example                              |
|----------------------|-------------------------------------|--------------------------------------|
| Classes              | PascalCase                          | `ChromosomePool`, `RegressionFitness` |
| Interfaces           | `I` + PascalCase                    | `ISymbol`, `IFitnessFunction`        |
| Methods              | PascalCase (verb + noun)            | `Evaluate`, `AddVariable`            |
| Properties           | PascalCase                          | `HeadLength`, `Fitness`              |
| Private fields       | `_camelCase`                        | `_sequence`, `_fitnessFunction`      |
| Local variables      | `camelCase`                         | `geneIndex`, `totalError`            |
| Constants            | PascalCase                          | `ExtractPath`                        |
| File names           | Match the primary type when practical | `Selection.cs`                     |

- Use descriptive names. Short names already in the library (`Parms`, `syms`, `rng`, `idx`) stay as they are. Do not rename them in passing.
- Boolean properties in new code should start with `Is`, `Has`, `Can`, or `Should`.

---

## 4. Code Style Rules

### Braces (Always Required)
**Always** surround code blocks with braces, even when the block contains only one statement.

**Correct:**
```csharp
if (x > 0)
{
    throw new ArgumentException("x must be greater than zero");
}
```

**Incorrect:**
```csharp
if (x > 0)
    throw new ArgumentException("x must be greater than zero");
```

This rule applies to new `if`, `else`, `for`, `foreach`, `while`, `do`, `switch`, `try`, `catch`, and `finally` blocks. Existing guards in `Gene` and `GeneticOperations` sometimes omit braces. Do not reformat those unless you are already changing that statement.

### Never Use `goto`
The `goto` statement is **forbidden** in all code.

---

## 5. Class Member Ordering

Classes **must** follow this strict member ordering:

1. **Constructors** (first)
2. **Public members**
3. **Protected members** (wrapped in `#region protected`)
4. **Private members** (wrapped in `#region private`)

**Example:**

```csharp
public sealed class MyClass
{
    public MyClass()
    {
    }

    public int Calculate(int x)
    {
        // public implementation
    }

    #region protected

    protected virtual void OnCalculated()
    {
        // protected implementation
    }

    #endregion

    #region private

    private int _count;

    private void InternalHelper()
    {
        // private implementation
    }

    #endregion
}
```

This ordering and region usage must be followed consistently across the codebase.

---

## 6. Language Features

Match the code that is already here. `Parms*` types stay mutable classes with a parameterless constructor and settable properties. Do not introduce records, primary constructors, or `required`/`init` rewrites unless asked.

The library already uses ordinary `switch` statements, `ArrayPool<T>` on the transposition path, and `Parallel.ForEach` for fitness. Prefer those over new framework features. There is no async I/O in this repo.

---

## 7. Error Handling & Resilience

- Never swallow exceptions silently.
- This repo logs with `Console.WriteLine` (`Population.Run` and `gep_console`). Do not introduce `ILogger<T>`.
- Prefer clear, specific exceptions (`ArgumentException`, `InvalidOperationException`). The existing code also throws `Exception` for a bad test-case index.
- Do not add retry or resilience libraries. There is no external call to retry.

---

## 8. Asynchronous Programming

`Pgep` and `gep_console` are synchronous. Fitness uses `Parallel.ForEach`. The host loads CSV through DynamicTable on the calling thread. Do not convert the evolution loop or the host to `async`/`CancellationToken` unless asked. If async is added later, do not block on `.Result` or `.Wait()`.

---

## 9. Performance & Memory

- Be mindful of allocations in hot paths.
- Use `Span<T>`, `ReadOnlySpan<T>`, and `stackalloc` where appropriate and safe.
- Use `ArrayPool<T>` and `MemoryPool<T>` for high-frequency buffer scenarios.
- Avoid multiple enumerations of `IEnumerable<T>` in performance-critical code.
- Measure before optimizing. Readability and correctness take priority unless profiling demonstrates a clear need.

---

## 10. Collections

- Prefer `IReadOnlyList<T>`, `IReadOnlyCollection<T>`, or `IEnumerable<T>` in public APIs when mutation is not required.
- Use immutable collections from the BCL when they are already available in the project.
- Be intentional when calling `ToList()` or `ToArray()`.

---

## 11. Construction

There is no dependency-injection container and no generic host. Callers construct `Parms*` objects and pass them into `SymbolLibraryBuilder`, `ChromosomePool`, and `Population`. Do not add a service container.

---

## 12. Security

This repository has no database, authentication layer, or secret store. Do not add SQL, auth, or committed data files. The host extract path `D:\predict\index_extract.csv` stays a local path in `Program.cs`. Do not commit a copy of that extract.

---

## 13. Testing

There is no test project. `dotnet test` is not wired up. Do not add a test project unless asked. If tests are added later, put them in a separate project (not inside `Pgep`), name them `MethodName_StateUnderTest_ExpectedBehavior`, and use Arrange-Act-Assert.

---

## 14. Documentation & Comments

- Write self-documenting code as the primary goal.
- Add XML documentation when you add a public type or method. Today only `KarvaDecoder` has it. Do not backfill the rest unless asked.
- Add comments only to explain *why*, not *what*.
- Keep comments current — remove outdated ones.

---

## 15. Conflict Resolution

When rules appear to conflict, apply this priority:

1. **Correctness & Robustness**
2. **Readability & Maintainability**
3. **Performance** (only when justified by measurement)

---

**Last Updated**: November 2026  
**Review Cadence**: Update this document when the project targets or the public contracts change.
**Version**: 1.1