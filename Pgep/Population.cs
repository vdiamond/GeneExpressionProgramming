using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Pgep
{
    public class Population
    {
        public Population(ParmsPopulation pp, SymbolLibrary syms, ChromosomePool pool)
        {
            if (pp.RandomSeed.HasValue)
            {
                _rng = new Random(pp.RandomSeed.Value);
            }
            else
            {
                _rng = new Random();
            }
            _syms = syms;
            _pool = pool;
            _fitnessFunction = pp.FitnessFunction;
            _chromosomes = new List<Chromosome>();
            _workChromosomes = new List<Chromosome>();
            _populationSize = 0;
            _decoder = new KarvaDecoder(syms.Symbols, "<-Link->");
            _maxDegreeOfParallelism = pp.MaxDegreeOfParallelism;
            // Decoder link defaults to "+" (common for GEP regression). If your LinkingFunction
            // is e.g. product or other, you can set _decoder.LinkOperator = "*"; etc. for display.
        }
        public void Initialize(ParmsPopulationInitialization ppi)
        {
            _populationSize = ppi.PopulationSize;
            _constantMin = ppi.ConstantMin;
            _constantMax = ppi.ConstantMax;
            _releaseList(_chromosomes);
            _releaseList(_workChromosomes);

            for (int i = 0; i < _populationSize; i++)
            {
                var chrom = _pool.Get();

                for (int g = 0; g < chrom.GeneCount; g++)
                {
                    var gene = chrom.GetGene(g);
                    var sequence = gene.Sequence;

                    // populate the header
                    for (int h = 0; h < gene.HeadLength; h++)
                    {
                        if (ppi.InitialSequence != null && ppi.InitialSequence.Length > 0 && h < ppi.InitialSequence.Length)
                        {
                            sequence[h] = ppi.InitialSequence[h];
                            continue;
                        }
                        if (_rng.NextDouble() < 0.5)
                        {
                            sequence[h] = _syms.FunctionIndices[_rng.Next(_syms.FunctionIndices.Count)];
                        }
                        else
                        {
                            sequence[h] = _syms.TerminalIndices[_rng.Next(_syms.TerminalIndices.Count)];
                        }
                    }

                    // Tail: terminals only
                    for (int t = gene.HeadLength; t < gene.Length; t++)
                    {
                        sequence[t] = _syms.TerminalIndices[_rng.Next(_syms.TerminalIndices.Count)];
                    }

                    // Initialize Random Constants
                    var constants = gene.Constants;
                    for (int c = 0; c < constants.Length; c++)
                    {
                        constants[c] = _constantMin + _rng.NextDouble() * (_constantMax - _constantMin);
                    }
                }
                chrom.Fitness = 0.0;
                _chromosomes.Add(chrom);
            }
        }
        public void Evolve(ParmsEvolution pe)
        {
            // Pre-Condition: _chromosomes is sorted in descending order of Fitness
            _releaseList(_workChromosomes);

            // Elitism
            for (int e = 0; e < Math.Min(pe.ElitismCount, _chromosomes.Count); e++)
            {
                var chrom = _pool.Get();
                _chromosomes[e].CopyTo(chrom);
                _workChromosomes.Add(chrom);
            }

            while (_workChromosomes.Count < _populationSize)
            {
                // Select two parents
                var parent1 = Selection.Tournament(_chromosomes, pe.TournamentSize, _rng);
                var parent2 = Selection.Tournament(_chromosomes, pe.TournamentSize, _rng);

                var child1 = _pool.Get();
                parent1.CopyTo(child1);

                var child2 = _pool.Get();
                parent2.CopyTo(child2);

                // Apply operators probabilistically
                double r = _rng.NextDouble();

                if (r < pe.OnePointCrossoverRate)
                {
                    GeneticOperations.OnePointCrossover(child1.GeneSequences, child2.GeneSequences, pe.HeaderFreeze, _rng);
                }
                else
                {
                    if (r < pe.OnePointCrossoverRate + pe.TwoPointCrossoverRate)
                    {
                        GeneticOperations.TwoPointCrossover(child1.GeneSequences, child2.GeneSequences, pe.HeaderFreeze, _rng);
                    }
                    else
                    {
                        if (r < pe.OnePointCrossoverRate + pe.TwoPointCrossoverRate + pe.GeneCrossoverRate)
                        {
                            GeneticOperations.GeneCrossover(child1.GeneSequences, child2.GeneSequences, pe.HeaderFreeze, _rng);
                        }
                    }
                }

                // Apply other operators to children
                GeneticOperations.Mutation(child1.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MutationRate, _syms.FunctionIndices, _syms.TerminalIndices, _rng);
                GeneticOperations.MutationConstants(child1.GeneConstants, pe.ConstantMutationRate, _constantMin, _constantMax, _rng);
                GeneticOperations.Mutation(child2.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MutationRate, _syms.FunctionIndices, _syms.TerminalIndices, _rng);
                GeneticOperations.MutationConstants(child2.GeneConstants, pe.ConstantMutationRate, _constantMin, _constantMax, _rng);

                if (_rng.NextDouble() < pe.ISTranspositionRate)
                {
                    GeneticOperations.ISTransposition(child1.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MaxTransposonLength, _rng);
                    GeneticOperations.ISTransposition(child2.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MaxTransposonLength, _rng);
                }

                if (_rng.NextDouble() < pe.RISTranspositionRate)
                {
                    GeneticOperations.RISTransposition(child1.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MaxTransposonLength, _rng);
                    GeneticOperations.RISTransposition(child2.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, pe.MaxTransposonLength, _rng);
                }

                //debug_check_chromosome("before", child1);
                //debug_check_chromosome("before", child2);


                if (_rng.NextDouble() < pe.GeneTranspositionRate)
                {
                    GeneticOperations.GeneTransposition(child1.GeneSequences, child1.GeneConstants, pe.HeaderFreeze, _rng);
                    GeneticOperations.GeneTransposition(child2.GeneSequences, child2.GeneConstants, pe.HeaderFreeze, _rng);
                }

                if (_rng.NextDouble() < pe.InversionRate)
                {
                    GeneticOperations.Inversion(child1.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, _rng);
                    GeneticOperations.Inversion(child2.GeneSequences, pe.HeaderFreeze, _pool.HeadLength, _rng);
                }

                //debug_check_chromosome("after", child1);
                //debug_check_chromosome("after", child2);

                _workChromosomes.Add(child1);
                if (_workChromosomes.Count < _populationSize)
                {
                    _workChromosomes.Add(child2);
                }
            }
            _implementWorkList();
        }
        public void Run(ParmsRunner pr, ParmsEvolution pe, IList<double> expectedOutputs, ITestCaseRange testCaseRange)
        {
            for (int gen = 0; gen < pr.MaxGenerations; gen++)
            {
                EvaluatePopulationFitness(expectedOutputs, testCaseRange);
                var g = this[0];
                Console.WriteLine($"Gen {gen,3} Best fitness: {g.Fitness} Expr: {_decoder.Decode(g).Substring(1,Math.Min(50, _decoder.Decode(g).Length - 1))}");
                if (g.Fitness >= pr.FitnessThreshold)
                {
                    Console.WriteLine("Fitness threshold met. Stopping evolution.");
                    return;
                };
                Evolve(pe);
            }
            Console.WriteLine($"Maximum Generations({pr.MaxGenerations}) Reached. Stopping evolution");
        }
        public void Release()
        {
            _releaseList(_chromosomes);
            _releaseList(_workChromosomes);
        }

        public double EvaluatePopulationFitness(IList<double> expectedOutputs, ITestCaseRange range)
        {
            var options = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(0, Math.Min(_maxDegreeOfParallelism, Environment.ProcessorCount))
            };

            Parallel.ForEach(_chromosomes, options, chrom =>
            {
                chrom.Fitness = _fitnessFunction.Evaluate(chrom, expectedOutputs, range);
            });

            // All evaluations are guaranteed to be finished here
            _chromosomes.Sort((p1, p2) => p2.Fitness.CompareTo(p1.Fitness));
            return _chromosomes[0].Fitness;
        }

        public IFitnessFunction FitnessFunction { get { return _fitnessFunction; } }

        public Chromosome this[int i] { get { return _chromosomes[i]; } }
        public int Count { get { return _chromosomes.Count; } }


        #region private
        private void _releaseList(List<Chromosome> list)
        {
            foreach (var chrom in list) { _pool.Return(chrom); }
            list.Clear();
        }
        private void _implementWorkList()
        {
            _releaseList(_chromosomes);
            for (int i = 0; i < _workChromosomes.Count; i++)
            {
                _chromosomes.Add(_workChromosomes[i]);
            }
            _workChromosomes.Clear();
        }

        private IFitnessFunction _fitnessFunction;
        private KarvaDecoder _decoder;
        private SymbolLibrary _syms;
        private Random _rng;
        private List<Chromosome> _chromosomes;
        private List<Chromosome> _workChromosomes;
        private ChromosomePool _pool;
        private int _populationSize;
        private double _constantMin;
        private double _constantMax;
        private int _maxDegreeOfParallelism;
        private void debug_check_chromosome(string place, Chromosome chrom)
        {
            for (int g = 0; g < chrom.GeneCount; g++)
            {
                var gene = chrom.GetGene(g);
                var sequence = gene.Sequence;
                for (int i = 0; i < sequence.Length; i++)
                {
                    if (sequence[i] < 0 || sequence[i] >= _syms.Symbols.Count)
                    {
                        Console.WriteLine($"Invalid symbol index {sequence[i]} in chromosome at {place}.");
                    }
                    if (i >= gene.HeadLength)
                    {
                        var symType = _syms.Symbols[sequence[i]].Type;
                        if (symType == SymbolType.Function)
                        {
                            Console.WriteLine($"Invalid function symbol index {sequence[i]} in tail of chromosome at {place}.");
                        }
                    }
                }
            }
        }

        #endregion
    }

    public interface IFitnessFunction
    {
        string Name { get; }
        double Evaluate(Chromosome chromosome, IList<double> expectedOutputs, ITestCaseRange range);
    }

    public class RegressionFitness : IFitnessFunction
    {
        public string Name => "Regression (1/(1+MSE))";

        public double Evaluate(Chromosome chromosome, IList<double> expectedOutputs, ITestCaseRange range)
        {
            // PreCondition: expectedOutputs.Length == _syms.TestCaseCount
            double totalError = 0.0;
            int validCount = 0;

            for (var i = 0; i < range.Count(); i++)
            {
                var actual = chromosome.Evaluate(range[i]);

                double error = actual - expectedOutputs[range[i]];
                totalError += error * error;
                validCount++;
            }

            if (validCount == 0)
            {
                return 0.0;
            }

            double mse = totalError / validCount;
            var ret = 1.0 / (1.0 + mse);
            if (double.IsNaN(ret) || double.IsInfinity(ret))
            {
                return 0.0;
            }
            return ret;
        }
    }

    public interface ITestCaseRange
    {
        int this[int i] { get; }
        int Count();
    }

    public class TestCaseRange : ITestCaseRange
    {
        public TestCaseRange(int start, int count) 
        {
            _count = count;
            _start = start;
         }
        public int Count()
        {
            return _count;
        }
        public int this[int i] {  get { if (i < 0 || i >= _count) { throw new Exception("Index out of range (TestCaseRange)"); } return _start + i; } }
        #region private
        private int _start;
        private int _count;
        #endregion
    }


}
