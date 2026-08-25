using System;
using System.Collections.Generic;
using System.Text;

namespace Pgep
{
    // Used by - SymbolLibraryBuilder
    public class ParmsSymbolLibrary
    {
        public ParmsSymbolLibrary() { }
        public int NumberConstants { get; set; } = 2;
        public PredefinedLibrary PredefinedLibraries { get; set; }
    }

    // Used by - ChromosomePool
    public class ParmsChromosome
    {
        public ParmsChromosome() { }
        public int ChromosomePoolSize { get; set; } = 100;
        public int NumberGenes { get; set; } = 2;
        public int HeadLength { get; set; } = 8;
        public Func<double[], double> LinkingFunction { get; set; } = values => values.Sum();

    }

    // Used by - Population
    public class ParmsPopulation
    {
        public ParmsPopulation() { }
        public int? RandomSeed { get; set; } = null;
        public IFitnessFunction FitnessFunction { get; set; } = new RegressionFitness();
        public int MaxDegreeOfParallelism { get; set; } = 1;
    }

    // Used by - Population.Initialize
    public class ParmsPopulationInitialization
    {
        public ParmsPopulationInitialization() { }
        public int PopulationSize { get; set; } = 100;
        public double ConstantMin { get; set; } = -10.0;
        public double ConstantMax { get; set; } = 10.0;
        public int[]? InitialSequence { get; set; } = null;
    }

    // Used by - Population.Evolve
    public class ParmsEvolution
    {
        public ParmsEvolution() { }
        public double MutationRate { get; set; } = 0.044;
        public double OnePointCrossoverRate { get; set; } = 0.3;
        public double TwoPointCrossoverRate { get; set; } = 0.2;
        public double GeneCrossoverRate { get; set; } = 0.1;
        public double ISTranspositionRate { get; set; } = 0.1;
        public double RISTranspositionRate { get; set; } = 0.1;
        public double GeneTranspositionRate { get; set; } = 0.1;
        public double InversionRate { get; set; } = 0.1;
        public double ConstantMutationRate { get; set; } = 0.1;
        public int MaxTransposonLength { get; set; } = 3;
        public int TournamentSize { get; set; } = 3;
        public int ElitismCount { get; set; } = 1;
        public int HeaderFreeze { get; set; } = 0;
    }

    // Used by - Runner
    public class ParmsRunner
    {
        public ParmsRunner() { }
        public double FitnessThreshold { get; set; } = 0.99;
        public int MaxGenerations { get; set; } = 500;
    }
}
