using Pgep;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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



class Program
{
    static IList<double> KarvaSPXLoad(SymbolLibraryBuilder sb, out IReadOnlyList<string> date)
    {
        //
        // Load Csv - convert all fields to the correct types
        var attr = new DefaultFieldAttributes();
        attr.SetDefault(TypeCode.Double);
        attr.Add(new Field("Date", TypeCode.String, true));
        IDynamicTable extractTable = new DynamicTable();
        extractTable.LoadCsv(@"d:\predict\index_extract_ta.csv", CsvHeaderType.Header, null, attr);

        date = extractTable.AsList<string>("Date").AsReadOnly();

        var qqq_open = extractTable.AsList<double>("qqq_open").AsReadOnly();
        var qqq_low = extractTable.AsList<double>("qqq_low").AsReadOnly();
        var qqq_high = extractTable.AsList<double>("qqq_high").AsReadOnly();
        var qqq_close = extractTable.AsList<double>("qqq_close").AsReadOnly();
        var qqq_vol = extractTable.AsList<double>("qqq_vol").AsReadOnly();
        var qqq_ema9 = extractTable.AsList<double>("qqq_ema9").AsReadOnly();
        var qqq_ema20 = extractTable.AsList<double>("qqq_ema20").AsReadOnly();
        var qqq_ema50 = extractTable.AsList<double>("qqq_ema50").AsReadOnly();
        var qqq_slope9 = extractTable.AsList<double>("qqq_slope9").AsReadOnly();
        var qqq_slope20 = extractTable.AsList<double>("qqq_slope20").AsReadOnly();
        var qqq_slope50 = extractTable.AsList<double>("qqq_slope50").AsReadOnly();
        var qqq_rsi14 = extractTable.AsList<double>("qqq_rsi14").AsReadOnly();
        var qqq_adx14 = extractTable.AsList<double>("qqq_adx14").AsReadOnly();
        var qqq_volatility20 = extractTable.AsList<double>("qqq_volatility20").AsReadOnly();
        var qqq_macd_hist26_12_9 = extractTable.AsList<double>("qqq_macd_hist26_12_9").AsReadOnly();
        var qqq_macd_signal26_12_9 = extractTable.AsList<double>("qqq_macd_signal26_12_9").AsReadOnly();
        var qqq_bollinger_width20_2 = extractTable.AsList<double>("qqq_bollinger_width20_2").AsReadOnly();
        var qqq_bollinger_pos20_2 = extractTable.AsList<double>("qqq_bollinger_pos20_2").AsReadOnly();
        var qqq_support = extractTable.AsList<double>("qqq_support").AsReadOnly();
        var qqq_support_strength = extractTable.AsList<double>("qqq_support_strength").AsReadOnly();
        var qqq_resistance = extractTable.AsList<double>("qqq_resistance").AsReadOnly();
        var qqq_resistance_strength = extractTable.AsList<double>("qqq_resistance_strength").AsReadOnly();
        var qqq_yield = extractTable.AsList<double>("qqq_yield").AsReadOnly();
        var qqq_yield_la1 = extractTable.AsList<double>("qqq_yield_la1").AsReadOnly();

        sb.AddVariable("qqq_ema9", qqq_ema9);
        sb.AddVariable("qqq_ema20", qqq_ema20);
        sb.AddVariable("qqq_ema50", qqq_ema50);
        sb.AddVariable("qqq_slope9", qqq_slope9);
        sb.AddVariable("qqq_slope20", qqq_slope20);
        sb.AddVariable("qqq_slope50", qqq_slope50);
        sb.AddVariable("qqq_rsi14", qqq_rsi14);
        sb.AddVariable("qqq_adx14", qqq_adx14);
        sb.AddVariable("qqq_volatility20", qqq_volatility20);
        sb.AddVariable("qqq_macd_hist26_12_9", qqq_macd_hist26_12_9);
        sb.AddVariable("qqq_macd_signal26_12_9", qqq_macd_signal26_12_9);
        sb.AddVariable("qqq_bollinger_width20_2", qqq_bollinger_width20_2);
        sb.AddVariable("qqq_bollinger_pos20_2", qqq_bollinger_pos20_2);
        sb.AddVariable("qqq_support", qqq_support);
        sb.AddVariable("qqq_support_strength", qqq_support_strength);
        sb.AddVariable("qqq_resistance", qqq_resistance);
        sb.AddVariable("qqq_resistance_strength", qqq_resistance_strength);
        sb.AddVariable("qqq_yield", qqq_yield);
        sb.AddVariable("qqq_yield_la1", qqq_yield_la1);

        var y = qqq_yield_la1;

        return y;
    }

    static void PrintPrediction(Chromosome chrom, IReadOnlyList<string> dte, IList<double> expected, TestCaseRange tc)
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
        var y = KarvaSPXLoad(symb, out date);

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
        //        var testCaseRange = new TestCaseRange(0, y.Count);
        //        var testCaseRange = new TestCaseRange(2000, 350);
        var testCaseRange = new TestCaseRange(y.Count - 1020, 1000);
        var decoder = new KarvaDecoder(syms.Symbols);
        var unseen = new TestCaseRange(y.Count - 20, 20);

        // =============================================================
        var pr = new ParmsRunner()
        {
            FitnessThreshold = 1.0,
            MaxGenerations = 100000
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