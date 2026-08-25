# Architecture

**Purpose**  
This document describes the architecture of the Gene Expression Programming (GEP) solution. All code — whether written by humans or AI agents — must follow these rules when changing project structure, chromosome/gene contracts, symbol libraries, pooling, evaluation, fitness, or evolutionary operators.

**Scope**  
Applies to the `Pgep` library and its host console application. Read this file before changing any of those areas.

**Companion documents**

- `AGENTS.md` — agent workflow and non-negotiable process rules
- `coding-standards.md` — C# style, member ordering, braces, and BCL-only library policy

---

## 1. What This System Is

This repository implements **Gene Expression Programming** (Ferreira): a genetic algorithm whose individuals are linear chromosomes encoded in **Karva notation** (K-expressions). Each gene is a fixed-length string that always decodes to a valid expression tree. Multiple genes on a chromosome are combined by a **linking function**.

Typical uses in this repo:

- Symbolic regression (`x² + 2x + 1`, `3x² + 25x − 1`)
- Boolean-style problems (XOR)
- Time-series / market-feature regression (QQQ experiments in the host app)

The engine is a **class library**. Experiments, CSV loading, and console reporting live in the host project.

---

## 2. Solution Layout

```
GeneExpressionProgramming/
├── .grok/rules/          Agent and architecture rules (this file lives here)
├── Pgep/                 Core GEP library (no I/O, no host data formats)
└── gep_console/          Console host / experiment runner
```

| Project | Kind | Role |
|---------|------|------|
| `Pgep` | Class library (`net10.0`) | Genome, symbols, evaluation, evolution, fitness contracts |
| `gep_console` | Console exe (`net10.0`) | Host: load CSV, configure a run, print results (currently a QQQ/yield experiment) |

**External sibling dependency (host only):** `DynamicTable` (`Table`, `TableCsv`, `TableExtension`) for CSV load and typed column access. **Do not** take a `Pgep` project reference to DynamicTable.

There is currently **no test project**. Prefer adding tests under a dedicated `Pgep.Tests` (or similar) project rather than embedding experiments in the library.

---

## 3. Layering

```
┌─────────────────────────────────────────────────────────────┐
│  Host (gep_console)                                         │
│  CSV load, variable selection, Parms* setup, Run, print     │
└──────────────────────────────┬──────────────────────────────┘
                               │ uses
┌──────────────────────────────▼──────────────────────────────┐
│  Pgep                                                       │
│                                                             │
│  Parameters  →  Symbols  →  Genome  →  Evolution  → Fitness │
│  Parms*         ISymbol      Gene        Population   IFitnessFunction
│                 Library      Chromosome  GeneticOps   ITestCaseRange
│                 Builder      Pool        Selection    RegressionFitness
│                              KarvaDecoder
└─────────────────────────────────────────────────────────────┘
                               │ uses
┌──────────────────────────────▼──────────────────────────────┐
│  .NET BCL only (Pgep)                                       │
│  arrays, ArrayPool, Parallel.ForEach, Random                │
└─────────────────────────────────────────────────────────────┘
```

**Dependency rule:** the host may depend on `Pgep` and DynamicTable. `Pgep` must not depend on the host, DynamicTable, files, or network I/O.

`Population.Run` currently writes to `Console`. Treat that as a host-facing convenience, not a reason to add more I/O to the library. Prefer keeping evolution/evaluation APIs side-effect free (`Initialize`, `EvaluatePopulationFitness`, `Evolve`) and leaving reporting in the host.

---

## 4. Pgep Type Map

All types live in namespace `Pgep`.

### 4.1 Parameters (`Parameters.cs`)

Configuration is **not** a single options bag. Each subsystem has its own `Parms*` object:

| Type | Consumed by | Responsibility |
|------|-------------|----------------|
| `ParmsSymbolLibrary` | `SymbolLibraryBuilder.Create` | Constant count; predefined function packs |
| `ParmsChromosome` | `ChromosomePool` | Pool size, gene count, head length, linking function |
| `ParmsPopulation` | `Population` ctor | RNG seed, fitness function, parallelism |
| `ParmsPopulationInitialization` | `Population.Initialize` | Population size, constant range, optional head seed |
| `ParmsEvolution` | `Population.Evolve` | Operator rates, tournament, elitism, header freeze |
| `ParmsRunner` | `Population.Run` | Fitness threshold, max generations |

Do not collapse these into one mega-options class. When adding a knob, put it on the `Parms*` type that owns that lifecycle step.

### 4.2 Symbols (`SymbolLibrary.cs`)

| Type | Role |
|------|------|
| `SymbolType` | `Function`, `Variable`, `Constant` |
| `ISymbol` | `Name`, `Arity`, `Type`, `Evaluate(double[] args, int caseIndex)` |
| `Function` / `Variable` / `Constant` | Concrete symbols |
| `PredefinedLibrary` | Flags: `BasicArithmetic`, `Trigonometric`, `Logical`, `Conditional`, `Neural` |
| `SymbolLibraryBuilder` | Mutable builder; enforces unique names and equal variable case counts |
| `SymbolLibrary` | Immutable after `Build()`; index lists for functions vs terminals |

**Symbol order in the built library is part of the genome contract:** functions, then variables, then constants. Sequence values on a gene are **indices into `SymbolLibrary.Symbols`**, not names.

**Arity contract**

- Functions: arity ≥ 1 (current predefined set uses 1, 2, or 4)
- Variables and constants: arity 0 (terminals)
- `MaxArity` is the maximum function arity in the library and drives **tail length**

**Evaluation contract for `ISymbol.Evaluate`**

- Function: `args` is the operand buffer (length ≥ arity)
- Variable: `args` is unused; `caseIndex` selects the sample
- Constant: `args` is the **gene’s constant array**; `caseIndex` is unused; the symbol’s `Index` selects which constant

Do not change this calling convention without updating `Gene.Evaluate` and `KarvaDecoder` together.

### 4.3 Genome (`Chromosome.cs`)

| Type | Role |
|------|------|
| `Gene` | One K-expression: `Sequence` (int indices) + `Constants` (doubles) |
| `Chromosome` | `N` genes + linking function + `Fitness` |
| `ChromosomePool` | Recycles chromosomes of a **fixed shape** |

### 4.4 Evolution (`Population.cs`, `GeneticOperations.cs`, `Selection.cs`)

| Type | Role |
|------|------|
| `Population` | Initialize, evaluate, evolve, run, release to pool |
| `GeneticOperations` | In-place operators on `int[][]` / `double[][]` views |
| `Selection` | Tournament (used); roulette currently unused / not a true fitness-proportional wheel |

### 4.5 Fitness and ranges (`Population.cs`)

| Type | Role |
|------|------|
| `IFitnessFunction` | `Evaluate(chromosome, expectedOutputs, range)` → higher is better |
| `RegressionFitness` | `1 / (1 + MSE)`; NaN/Inf → 0 |
| `ITestCaseRange` | Maps local `0..Count-1` to dataset row indices |
| `TestCaseRange` | Contiguous `[start, start+count)` |

### 4.6 Display (`KarvaEvaluator.cs`)

`KarvaDecoder` pretty-prints a gene or chromosome as infix/math text. It is **not** the runtime evaluator. Runtime evaluation is `Gene.Evaluate` / `Chromosome.Evaluate`.

The file is named `KarvaEvaluator.cs` for historical reasons. Do not introduce a second decoder. If you rename the file, match the primary type (`KarvaDecoder`).

---

## 5. Genome Invariants (Do Not Break)

These rules are the correctness core of GEP. Operators, initialization, and evaluation all depend on them.

### 5.1 Gene layout

```
[ -------- head (h) -------- | -------- tail (t) -------- ]
  functions OR terminals         terminals ONLY
```

- `h = HeadLength` (≥ 1)
- `t = h * (MaxArity - 1) + 1`
- `Length = h + t` (fixed for the run)
- Sequence values are symbol indices in `[0, Symbols.Count)`

**Head** may contain functions or terminals. **Tail** may contain only terminals (variables and constants). Initialization and mutation must preserve this. Transposition/inversion that operate on the head must **never write into the tail**.

### 5.2 Open reading frame (ORF)

The expressed tree is the prefix of the sequence whose arities consume exactly one result:

```
argCount = 1
for i = 0 .. Length-1:
    argCount += arity(sequence[i]) - 1
    stop when argCount == 0   →  sequenceEnd = i
```

Unused tail (and often unused head) is non-coding DNA. It must still be valid so that later operators can promote it into the ORF.

`Gene.Evaluate` walks **from `sequenceEnd` down to 0** with an explicit stack (postfix evaluation of the K-expression). `KarvaDecoder` walks **from the start** recursively. Both must agree on `sequenceEnd`.

### 5.3 Constants

Each gene owns a `double[]` of length `NumberConstants` (from `ParmsSymbolLibrary` / the pool). Constant symbols (`_c0`, `_c1`, …) index into **that gene’s** array.

Do not share constant arrays across genes. Gene transposition that swaps genes must swap **both** sequence and constants (`GeneticOperations.GeneTransposition` already does this).

### 5.4 Chromosome linking

```
value = LinkingFunction(gene0.Evaluate(i), gene1.Evaluate(i), …)
```

The linking function is a `Func<double[], double>` captured at pool/chromosome construction. Common host choices:

- Sum: `values => values.Sum()` (regression)
- Squashed sum: logistic of the sum (classification-style / bounded output)

`KarvaDecoder` joins gene strings with a **display** link operator. Display linking and runtime linking are independent; keep them consistent in the host when you change `LinkingFunction`.

### 5.5 Header freeze

`ParmsEvolution.HeaderFreeze` is a prefix length **inside each gene** that recombination/mutation/transposition must not mutate.

- `0` = entire gene is mutable (default)
- Operators treat the frozen prefix as read-only and compute mutable regions as `geneLength - headerFreeze`

If you add an operator, honor `headerFreeze` the same way existing operators do.

---

## 6. Runtime Evaluation

`Gene.Evaluate(idx, sequenceEnd = 0)`:

1. Resolve ORF end (`GetSequenceEnd()` unless a non-zero end is passed)
2. Walk the ORF **backwards**
3. Terminals push a value; functions pop `arity` args into `_argBuffer` and push the result
4. NaN / ±Infinity from a function is mapped to `0.0` (GEP convention in this codebase)
5. Exactly one stack value must remain

`Chromosome.Evaluate(index)` evaluates every gene at the same case index, then applies the linking function.

**Case index** is a row in the variable arrays. `ITestCaseRange` selects which rows count for fitness. Variables must all have the same length (`SymbolLibraryBuilder` enforces this).

Evaluation must stay **allocation-light** on the hot path: reuse `_stack` and `_argBuffer` on the gene; do not allocate per sample.

---

## 7. Evolution Pipeline

`Population.Run`:

```
for gen in 0 .. MaxGenerations-1:
    EvaluatePopulationFitness(expected, range)   // parallel, then sort descending
    if best.Fitness >= FitnessThreshold: stop
    Evolve(pe)
```

### 7.1 Fitness

- **Higher is better.** Sort is descending (`p2.Fitness.CompareTo(p1.Fitness)`).
- `EvaluatePopulationFitness` uses `Parallel.ForEach` with `MaxDegreeOfParallelism`.
- A chromosome’s `Evaluate` must be safe to run concurrently **across chromosomes**. Do not share mutable evaluator state between chromosomes.
- `RegressionFitness` returns `1 / (1 + MSE)` in `[0, 1]` for finite errors.
- Custom fitness (the host may implement `IFitnessFunction`) must still return a comparable non-NaN score; prefer `0` for invalid individuals.

### 7.2 Evolve

Precondition: `_chromosomes` is already sorted by fitness descending.

1. Copy the top `ElitismCount` individuals into the work list (via the pool)
2. Until the work list reaches population size:
   - Tournament-select two parents
   - Copy into two pooled children
   - Apply **one** recombination (one-point, two-point, or gene crossover) by stacked rates
   - Always mutate sequences and constants on both children
   - Probabilistically apply IS, RIS, gene transposition, inversion
3. Swap work list into `_chromosomes` and return the previous generation to the pool

Operators mutate **`GeneSequences` / `GeneConstants` in place**. Those arrays are the live storage inside each `Gene`. Do not replace the arrays; write into them.

### 7.3 Operators (`GeneticOperations`)

| Operator | Genome effect | Constraint |
|----------|---------------|------------|
| One-point crossover | Swap suffix of concatenated genes after a cut | Cut is in the unfrozen region |
| Two-point crossover | Swap the interior between two cuts | Same |
| Gene crossover | Swap one entire gene sequence | Does not swap constants |
| Mutation | Head: function or terminal; tail: terminal only | Frozen prefix skipped |
| Constant mutation | Perturb or re-sample a constant | Uses `ConstantMin` / `ConstantMax` from initialization |
| IS transposition | Insert a subsequence into the **head** (not position 0) | Shift stays inside the head; tail untouched |
| RIS transposition | Insert at the **start of the mutable head** | Tail untouched |
| Gene transposition | Swap gene 0 with another gene | Sequences **and** constants |
| Inversion | Reverse a segment of the mutable head | Tail untouched |

IS transposition rents from `ArrayPool<int>.Shared`. Keep operator helpers allocation-free on the hot path where practical.

`Selection.Tournament` is the production selector. Do not wire `RouletteWheel` without implementing real fitness-proportional selection (today it is uniform random).

---

## 8. Pooling

`ChromosomePool` recycles chromosomes of one **fixed shape**:

- gene count, head length, max arity, constant count, linking function, symbol library

`Get()` dequeues or constructs. `Return()` enqueues until `ChromosomePoolSize`. Excess returns are dropped (eligible for GC).

`Population` must `Return` every chromosome it no longer holds (`Initialize`, `Evolve` generation swap, `Release`). Never return a chromosome that is still referenced from `_chromosomes` or `_workChromosomes`.

Do not pool across runs with different `ParmsChromosome` / `SymbolLibrary` shapes. Create a new pool when shape changes.

---

## 9. Host Application Pattern

The host should follow this construction order (already used in `gep_console`):

1. `ParmsSymbolLibrary` → `SymbolLibraryBuilder.Create`
2. Add variables (from CSV or synthetic arrays) and the target series
3. `Build()` → `SymbolLibrary`
4. `ParmsChromosome` → `ChromosomePool`
5. `ParmsPopulation` → `Population`
6. `ParmsPopulationInitialization` → `Initialize`
7. Choose `ITestCaseRange` (train vs unseen holdout)
8. `ParmsRunner` + `ParmsEvolution` → `Run` (or a host-owned loop over evaluate/evolve)
9. `KarvaDecoder.Decode` for the best individual
10. `Population.Release` when the run is finished (the host should call this; add it if a new host forgets)

Keep experiment-specific fitness (`TestFitness` in the host) **out of `Pgep`** unless it is a reusable, named metric.

---

## 10. Rules for Changing the Architecture

### Allowed without a design discussion

- New `IFitnessFunction` implementations (prefer host unless broadly reusable)
- New functions via `SymbolLibraryBuilder.AddFunction` or a new `PredefinedLibrary` flag
- New host experiments
- Tests for existing contracts
- Bug fixes that preserve the invariants in §5

### Requires care — update this document if the contract changes

- Gene head/tail formula or sequence encoding
- `ISymbol.Evaluate` signature or constant vs variable argument meaning
- Chromosome linking
- Pool shape / `Get`/`Return` protocol
- Operator semantics, especially tail purity and header freeze
- Fitness “higher is better” convention
- Parallel evaluation assumptions

### Forbidden unless the user explicitly asks

- NuGet packages in `Pgep` (BCL only)
- DynamicTable, CSV, or file I/O inside `Pgep`
- A second genome representation alongside Karva
- `goto`
- Inheritance hierarchies for symbols or chromosomes (composition + interfaces only)
- Sharing a `Random` or other mutable static across parallel fitness workers

---

## 11. Extension Points

| Goal | How |
|------|-----|
| New math/boolean operator | `AddFunction` or extend `PredefinedLibrary` in `SymbolLibraryBuilder.Create` |
| New input feature | `AddVariable` in the host; same length as existing variables |
| New objective | Implement `IFitnessFunction` |
| New subset of rows | Implement `ITestCaseRange` (or use `TestCaseRange`) |
| New genetic operator | Static method on `GeneticOperations`; call it from `Population.Evolve`; add a rate on `ParmsEvolution` |
| New selection | Static method on `Selection`; swap the call in `Evolve` |
| Bounded / classified output | Change `ParmsChromosome.LinkingFunction` |

When adding a public type or method, add XML documentation.

---

## 12. Performance Constraints

The fitness inner loop is `populationSize × testCases × genes × ORF length`. Treat it as a hot path:

- No LINQ, no heap allocation per sample in `Evaluate` / fitness
- Reuse gene stack/arg buffers and chromosome link buffers
- Prefer `ArrayPool` for short-lived operator scratch
- Parallelize **across chromosomes**, not inside a single evaluation, unless you prove thread safety
- `KarvaDecoder` is for reporting, not for fitness

---

## 13. Coding Shape (Pgep)

Follow `coding-standards.md`. In this library that specifically means:

- Member order: constructors → public members → `#region protected` → `#region private`
- Braces on every block
- `I` prefix on interfaces; `_camelCase` private fields
- Fail fast (`ArgumentException` / `InvalidOperationException`) on malformed genomes and parameter misuse
- Keep `GeneticOperations` and `Selection` as static classes of pure functions over arrays / lists

`Parms*` types are mutable property bags with defaults. That is the established configuration style; do not replace them with required-init records unless a dedicated refactor is requested.

---

## 14. Current Gaps (Do Not Silently “Fix” Unless Asked)

These are known characteristics of the present code, not invitations to rewrite:

- No dedicated test project
- `Population.Run` writes to `Console`
- `Selection.RouletteWheel` is not a roulette wheel
- `KarvaEvaluator.cs` contains `KarvaDecoder`
- `gep_console` duplicates several experiment setups in one `Program.cs`

---

_Last Updated: August 2026_  
_Version: 1.0_
