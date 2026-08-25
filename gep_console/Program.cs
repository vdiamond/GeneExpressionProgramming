using Pgep;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Table;
using TableCsv;
using TableExtension;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class TestFitness : IFitnessFunction
{
    public string Name => "xxx";

    public double Evaluate(Chromosome chromosome, IList<double> expectedOutputs, ITestCaseRange range)
    {
        // PreCondition: expectedOutputs.Length == _syms.TestCaseCount
        double totalError = 0.0;
        int validCount = 0;

        for (int i = 0; i < range.Count(); i++)
        {
            var actual = chromosome.Evaluate(range[i]);

            double error = 0.0;
            if (actual >= 0 && expectedOutputs[range[i]] < 0) { error = 1.0; }
            if (actual <= 0 && expectedOutputs[range[i]] > 0) { error = 1.0; }
            totalError += error * error;
            validCount++;
        }

        if (validCount == 0)
        {
            return 0.0;
        }

        double mse = (validCount - totalError) / validCount;
        var ret = mse;
        if (double.IsNaN(ret) || double.IsInfinity(ret))
        {
            return 0.0;
        }
        return ret;
    }
}

public class IndexListRange : ITestCaseRange
{
    public IndexListRange(IReadOnlyList<int> indices)
    {
        _indices = indices ?? throw new ArgumentNullException(nameof(indices));
    }
    public int Count()
    {
        return _indices.Count;
    }
    public int this[int i]
    {
        get
        {
            if (i < 0 || i >= _indices.Count)
            {
                throw new Exception("Index out of range (IndexListRange)");
            }
            return _indices[i];
        }
    }
    private readonly IReadOnlyList<int> _indices;
}

class Program
{
    const string ExtractPath = @"D:\predict\index_extract.csv";

    static List<T> TakeAt<T>(IList<T> src, List<int> idx)
    {
        var dest = new List<T>(idx.Count);
        for (int k = 0; k < idx.Count; k++)
        {
            dest.Add(src[idx[k]]);
        }
        return dest;
    }

    static bool SameQqqOhlcv(IList<double> o, IList<double> l, IList<double> h, IList<double> c, IList<double> v, int i, int j)
    {
        return o[i] == o[j] && l[i] == l[j] && h[i] == h[j] && c[i] == c[j] && v[i] == v[j];
    }

    static int DateYear(string d)
    {
        if (DateTime.TryParse(d, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            return dt.Year;
        }
        if (!string.IsNullOrEmpty(d) && d.Length >= 4 && int.TryParse(d.Substring(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var y))
        {
            return y;
        }
        return -1;
    }

    static void AddIfPresent(SymbolLibraryBuilder sb, IDynamicTable table, List<int> cashIdx, string colName, List<string> featureNames)
    {
        if (!table.columns.ColumnExists(colName))
        {
            Console.WriteLine($"Feature skipped (not in extract): {colName}");
            return;
        }
        sb.AddVariable(colName, TakeAt(table.AsList<double>(colName), cashIdx));
        featureNames.Add(colName);
    }

    static IList<double> KarvaSPXLoad(SymbolLibraryBuilder sb, out IReadOnlyList<string> date, out IndexListRange trainRange, out IndexListRange unseenRange)
    {
        // Extract-only cash sessions. Do not load index_extract_ta.csv. Do not AddVariable qqq_yield_la1.
        var attr = new DefaultFieldAttributes();
        attr.SetDefault(TypeCode.Double);
        attr.Add(new Field("Date", TypeCode.String, true));
        // Empty surprise cells break double.Parse; those columns are not features.
        attr.Add(new Field("evt_cpi_surprise", TypeCode.String));
        attr.Add(new Field("evt_fomc_surprise", TypeCode.String));

        IDynamicTable extractTable = new DynamicTable();
        extractTable.LoadCsv(ExtractPath, CsvHeaderType.Header, null, attr);

        Console.WriteLine($"Loaded extract: {ExtractPath}");
        Console.WriteLine($"Extract rows: {extractTable.rows.Count}");

        var rawDate = extractTable.AsList<string>("Date");
        var qqqOpen = extractTable.AsList<double>("qqq.Open");
        var qqqLow = extractTable.AsList<double>("qqq.Low");
        var qqqHigh = extractTable.AsList<double>("qqq.High");
        var qqqClose = extractTable.AsList<double>("qqq.Close");
        var qqqVol = extractTable.AsList<double>("qqq.Volume");

        var cashIdx = new List<int>();
        int nRows = extractTable.rows.Count;
        for (int i = 0; i < nRows; i++)
        {
            if (i > 0 && SameQqqOhlcv(qqqOpen, qqqLow, qqqHigh, qqqClose, qqqVol, i, i - 1))
            {
                continue;
            }
            cashIdx.Add(i);
        }

        Console.WriteLine($"Cash sessions: {cashIdx.Count} (dropped {nRows - cashIdx.Count} QQQ OHLCV copies of prior row)");

        var dates = TakeAt(rawDate, cashIdx);
        var close = TakeAt(qqqClose, cashIdx);
        int n = close.Count;

        var yieldSame = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; i++)
        {
            yieldSame[i] = (i == 0 || close[i - 1] == 0.0 || double.IsNaN(close[i]) || double.IsNaN(close[i - 1]))
                ? double.NaN
                : close[i] / close[i - 1] - 1.0;
            y[i] = (i == n - 1 || close[i] == 0.0 || double.IsNaN(close[i]) || double.IsNaN(close[i + 1]))
                ? double.NaN
                : close[i + 1] / close[i] - 1.0;
        }

        var featureNames = new List<string>();
        sb.AddVariable("qqq.Open", TakeAt(qqqOpen, cashIdx));
        featureNames.Add("qqq.Open");
        sb.AddVariable("qqq.Low", TakeAt(qqqLow, cashIdx));
        featureNames.Add("qqq.Low");
        sb.AddVariable("qqq.High", TakeAt(qqqHigh, cashIdx));
        featureNames.Add("qqq.High");
        sb.AddVariable("qqq.Close", close);
        featureNames.Add("qqq.Close");
        sb.AddVariable("qqq.Volume", TakeAt(qqqVol, cashIdx));
        featureNames.Add("qqq.Volume");
        sb.AddVariable("qqq.Yield", yieldSame);
        featureNames.Add("qqq.Yield");

        AddIfPresent(sb, extractTable, cashIdx, "vix.Close", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "vix1y.Close", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "vix6m.Close", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "evt_cpi", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "evt_fomc", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "evt_opex", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "macro_cpi_yoy", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "macro_unemp", featureNames);
        AddIfPresent(sb, extractTable, cashIdx, "macro_real_rate", featureNames);

        Console.WriteLine("Features: " + string.Join(", ", featureNames));
        Console.WriteLine("Target: next cash-session yield (close[i+1]/close[i]-1). No qqq_yield_la1. No TA.");

        var train = new List<int>();
        var unseen = new List<int>();
        for (int i = 0; i < n; i++)
        {
            if (double.IsNaN(yieldSame[i]) || double.IsNaN(y[i]))
            {
                continue;
            }
            int year = DateYear(dates[i]);
            if (year == 2022)
            {
                unseen.Add(i);
            }
            else
            {
                train.Add(i);
            }
        }

        trainRange = new IndexListRange(train);
        unseenRange = new IndexListRange(unseen);
        Console.WriteLine($"Train cases (year != 2022, has yield and target): {train.Count}");
        Console.WriteLine($"Unseen cases (year == 2022, has yield and target): {unseen.Count}");

        date = dates.AsReadOnly();
        return y;
    }

    static void PrintPrediction(Chromosome chrom, IReadOnlyList<string> dte, IList<double> expected, ITestCaseRange tc)
    {
        Console.WriteLine("Expected\tPredicted");
        for (int i = 0; i < tc.Count(); i++)
        {
            Console.WriteLine($"TestCase({tc[i]}) Date: {dte[tc[i]]} Expected: {expected[tc[i]]}\tPredicted: {chrom.Evaluate(tc[i])}");
        }
    }

    static void KarvaSPX()
    {
        Console.WriteLine("\n\n==================================SPX Prediction=====================================================");


        // =============================================================
        var psl = new ParmsSymbolLibrary()
        {
            NumberConstants = 5,
            PredefinedLibraries = 
                PredefinedLibrary.BasicArithmetic | 
                PredefinedLibrary.Logical | 
                PredefinedLibrary.Conditional | 
                //PredefinedLibrary.Trigonometric | 
                PredefinedLibrary.Neural 
        };
        var symb = SymbolLibraryBuilder.Create(psl);

        IReadOnlyList<string> date;
        var y = KarvaSPXLoad(symb, out date, out var testCaseRange, out var unseen);

        var syms = symb.Build();

        // ============================================================
        var pc = new ParmsChromosome()
        {
            ChromosomePoolSize = 100,
            NumberGenes = 2,
            HeadLength = 16,
            LinkingFunction = values => 1.0 / (1.0 + Math.Exp(values.Sum() * -1.0)) - 0.5
        };
        var chromPool = new ChromosomePool(pc, syms);

        // ============================================================
        var pp = new ParmsPopulation()
        {
            RandomSeed = 42,
            FitnessFunction = new RegressionFitness(),
            //FitnessFunction = new TestFitness(),
            MaxDegreeOfParallelism = 30
        };
        var population = new Population(pp, syms, chromPool);

        // ============================================================
        var ppi = new ParmsPopulationInitialization()
        {
            PopulationSize = 100,
            ConstantMin = -10.0,
            ConstantMax = 10.0,
            InitialSequence = null
        };
        population.Initialize(ppi);

        // =============================================================
        var decoder = new KarvaDecoder(syms.Symbols);

        // =============================================================
        var pr = new ParmsRunner()
        {
            FitnessThreshold = 1.0,
            MaxGenerations = 1000
        };
        var pe = new ParmsEvolution()
        {
            MutationRate = 0.044,
            OnePointCrossoverRate = 0.3,
            TwoPointCrossoverRate = 0.2,
            GeneCrossoverRate = 0.1,
            ISTranspositionRate = 0.1,
            RISTranspositionRate = 0.1,
            GeneTranspositionRate = 0.1,
            InversionRate = 0.1,
            ConstantMutationRate = 0.21,
            MaxTransposonLength = 3,
            TournamentSize = 10,
            ElitismCount = 2,
            HeaderFreeze = 0
        };  

        population.Run(pr, pe, y, testCaseRange);

        var pr2 = new KarvaDecoder(syms.Symbols);
        Console.WriteLine($"Chrom:({0})Fitness:{population[0].Fitness} {pr2.Decode(population[0])}");

        Console.WriteLine("\n\nTraining set outcomes");
        Console.WriteLine("=======================");
        PrintPrediction(population[0], date, y, testCaseRange);

        Console.WriteLine("\n\nPrediction set outcomes");
        Console.WriteLine("=========================");
        PrintPrediction(population[0], date, y, unseen);

    }






    static void KarvaTest3()
    {
        Console.WriteLine("\n\n==================================(3x^2 + 25x - 1)=====================================================");


        // =============================================================
        var psl = new ParmsSymbolLibrary()
        {
            NumberConstants = 4,
            PredefinedLibraries = PredefinedLibrary.BasicArithmetic
        };
        var symb = SymbolLibraryBuilder.Create(psl);
        
        var x = new double[40];
        var y = new double[40];
        double diff = 2.0;
        for (int i = 0; i < 40; i++)
        {
            x[i] = i * diff;
            y[i] = 3 * Math.Pow(x[i], 2) + 25 * x[i] - 1;
        }

        symb.AddVariable("x", x);
        var syms = symb.Build();

        // ============================================================
        var pc = new ParmsChromosome()
        {
            ChromosomePoolSize = 100,
            NumberGenes = 2,
            HeadLength = 16,
            LinkingFunction = values => values.Sum()
        };
        var chromPool = new ChromosomePool(pc, syms);

        // ============================================================
        var pp = new ParmsPopulation()
        {
            RandomSeed = 42,
             FitnessFunction = new RegressionFitness()
        };
        var population = new Population(pp, syms, chromPool);

        // ============================================================
        var ppi = new ParmsPopulationInitialization()
        {
            PopulationSize = 100,
            ConstantMin = -100.0,
            ConstantMax = 100.0,
            InitialSequence = null
        };
        population.Initialize(ppi);

        // =============================================================
        var testCaseRange = new TestCaseRange(0, y.Length);
        var decoder = new KarvaDecoder(syms.Symbols);

        // =============================================================
        var pr = new ParmsRunner()
        {
            FitnessThreshold = 1.0,
            MaxGenerations = 25000
        };
        var pe = new ParmsEvolution()
        {
            MutationRate = 0.044,
            OnePointCrossoverRate = 0.3,
            TwoPointCrossoverRate = 0.2,
            GeneCrossoverRate = 0.1,
            ISTranspositionRate = 0.1,
            RISTranspositionRate = 0.1,
            GeneTranspositionRate = 0.1,
            InversionRate = 0.1,
            ConstantMutationRate = 0.21,
            MaxTransposonLength = 3,
            TournamentSize = 3,
            ElitismCount = 2,
            HeaderFreeze = 0
        };

        population.Run(pr, pe, y, testCaseRange);
    }



    static void KarvaTest2()
    {
        Console.WriteLine("\n\n==================================(XOR)=====================================================");


        // =============================================================
        var psl = new ParmsSymbolLibrary()
        {
            NumberConstants = 2,
            PredefinedLibraries = PredefinedLibrary.BasicArithmetic | PredefinedLibrary.Trigonometric
        };
        var symb = SymbolLibraryBuilder.Create(psl);
  
        var x = new double[4] { 0.0, 1.0, 0.0, 1.0 };
        var y = new double[4] { 0.0, 0.0, 1.0, 1.0 };
        var z = new double[4] { 0.0, 1.0, 1.0, 0.0 };


        symb.AddVariable("x", x);
        symb.AddVariable("y", y);
        var syms = symb.Build();

        // ============================================================
        var pc = new ParmsChromosome()
        {
            ChromosomePoolSize = 100,
            NumberGenes = 2,
            HeadLength = 8,
            LinkingFunction = values => values.Sum()
        };
        var chromPool = new ChromosomePool(pc, syms);

        // ============================================================
        var pp = new ParmsPopulation()
        {
            RandomSeed = 42,
              FitnessFunction = new RegressionFitness()
        };
        var population = new Population(pp, syms, chromPool);

        // ============================================================
        var ppi = new ParmsPopulationInitialization()
        {
            PopulationSize = 100,
            ConstantMin = -10.0,
            ConstantMax = 10.0,
            InitialSequence = null
        };
        population.Initialize(ppi);

        // =============================================================
        var testCaseRange = new TestCaseRange(0, z.Length);
        var decoder = new KarvaDecoder(syms.Symbols);

        // =============================================================
        var pr = new ParmsRunner()
        {
            FitnessThreshold = 1.0,
            MaxGenerations = 50000
        };
        var pe = new ParmsEvolution()
        {
            MutationRate = 0.044,
            OnePointCrossoverRate = 0.3,
            TwoPointCrossoverRate = 0.2,
            GeneCrossoverRate = 0.1,
            ISTranspositionRate = 0.1,
            RISTranspositionRate = 0.1,
            GeneTranspositionRate = 0.1,
            InversionRate = 0.1,
            ConstantMutationRate = 0.1,
            MaxTransposonLength = 3,
            TournamentSize = 3,
            ElitismCount = 1,
            HeaderFreeze = 0
        };

        population.Run(pr, pe, z, testCaseRange);
    }
    static void KarvaTest1()
    {
        Console.WriteLine("\n\n==================================(x^2 + 2x + 1)=====================================================");

        // =============================================================
        var psl = new ParmsSymbolLibrary()
        {
            NumberConstants = 2,
            PredefinedLibraries = PredefinedLibrary.BasicArithmetic | PredefinedLibrary.Trigonometric
        };
        var symb = SymbolLibraryBuilder.Create(psl);

        var x = new double[21];
        var y = new double[21];
        int i = 0;
        for (double xx = -5.0; xx <= 5.0; xx += 0.5)
        {
            double yy = xx * xx + 2 * xx + 1;
            x[i] = xx;
            y[i] = yy;
            i++;
        }
        symb.AddVariable("x", x);
        var syms = symb.Build();

        // ============================================================
        var pc = new ParmsChromosome()
        {
            ChromosomePoolSize = 100,
            NumberGenes = 2,
            HeadLength = 8,
            LinkingFunction = values => values.Sum()
        };
        var chromPool = new ChromosomePool(pc, syms);

        // ============================================================
        var pp = new ParmsPopulation()
        {
            RandomSeed = 42,
              FitnessFunction = new RegressionFitness()
        };
        var population = new Population(pp, syms, chromPool);

        // ============================================================
        var ppi = new ParmsPopulationInitialization()
        {
            PopulationSize = 100,
            ConstantMin = -10.0,
            ConstantMax = 10.0,
            InitialSequence = null
        };
        population.Initialize(ppi);

        // =============================================================
        var testCaseRange = new TestCaseRange(0, y.Length);
        var decoder = new KarvaDecoder(syms.Symbols);

        // =============================================================
        var pr = new ParmsRunner()
        {
            FitnessThreshold = 1.0,
            MaxGenerations = 500
        };
        var pe = new ParmsEvolution()
        {
            MutationRate = 0.044,
            OnePointCrossoverRate = 0.3,
            TwoPointCrossoverRate = 0.2,
            GeneCrossoverRate = 0.1,
            ISTranspositionRate = 0.1,
            RISTranspositionRate = 0.1,
            GeneTranspositionRate = 0.1,
            InversionRate = 0.1,
            ConstantMutationRate = 0.1,
            MaxTransposonLength = 3,
            TournamentSize = 3,
            ElitismCount = 1,
            HeaderFreeze = 0
        };

        population.Run(pr, pe, y, testCaseRange);
    }

    static void Main(string[] args)
    {
        Stopwatch sw = new Stopwatch();
        sw.Start();

        //       KarvaTest1(); // x^2 + 2x + 1
        //       KarvaTest2(); // XOR
        //         KarvaTest3(); // 3x^2 + 25x - 1

        KarvaSPX();

        sw.Stop();
        Console.WriteLine($"Elapsed time: {sw.ElapsedMilliseconds} ms");
    }
}
