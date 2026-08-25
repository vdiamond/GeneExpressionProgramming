using System;
using System.Collections.Generic;
using System.Text;

namespace Pgep
{
    public static class Selection
    {
        // Tournament selection: pick k random, choose the best.
        public static Chromosome Tournament(List<Chromosome> chromosomes, int tournamentSize, Random rng)
        {
            double maxFitness = 0.0;
            Chromosome best = null;
            for (int t = 0; t < tournamentSize; t++)
            {
                var chrom = chromosomes[rng.Next(chromosomes.Count)];
                if (chrom.Fitness >= maxFitness)
                {
                    maxFitness = chrom.Fitness;
                    best = chrom;
                }
            }

            return best!;
        }

        // Roulette wheel selection 
        public static Chromosome RouletteWheel(List<Chromosome> chromosomes, Random rng)
        {
            return chromosomes[rng.Next(chromosomes.Count)];
        }
    }

}
