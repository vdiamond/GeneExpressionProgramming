
using System;
using System.Buffers;

namespace Pgep
{
    public static class GeneticOperations
    {
        // ============================================================
        // ===================== RECOMBINATION ========================
        // ============================================================

        public static void OnePointCrossover(int[][] chromosome1, int[][] chromosome2, int headerFreeze, Random rng)
        {
            int numGenes = chromosome1.Length;
            if (numGenes == 0) return;

            int geneLength = chromosome1[0].Length;
            if (headerFreeze >= geneLength) return;

            int mutablePerGene = geneLength - headerFreeze;
            int totalMutable = numGenes * mutablePerGene;

            int point = rng.Next(totalMutable);

            int geneIndex = point / mutablePerGene;
            int posInGene = point % mutablePerGene + headerFreeze;

            for (int i = posInGene; i < geneLength; i++)
            {
                int temp = chromosome1[geneIndex][i];
                chromosome1[geneIndex][i] = chromosome2[geneIndex][i];
                chromosome2[geneIndex][i] = temp;
            }

            for (int g = geneIndex + 1; g < numGenes; g++)
            {
                for (int i = 0; i < geneLength; i++)
                {
                    int temp = chromosome1[g][i];
                    chromosome1[g][i] = chromosome2[g][i];
                    chromosome2[g][i] = temp;
                }
            }
        }

        public static void TwoPointCrossover(int[][] chromosome1, int[][] chromosome2, int headerFreeze, Random rng)
        {
            int numGenes = chromosome1.Length;
            if (numGenes == 0) return;

            int geneLength = chromosome1[0].Length;
            if (headerFreeze >= geneLength) return;

            int mutablePerGene = geneLength - headerFreeze;
            int totalMutable = numGenes * mutablePerGene;

            int point1 = rng.Next(totalMutable);
            int point2 = rng.Next(totalMutable);

            if (point1 > point2) (point1, point2) = (point2, point1);
            if (point1 == point2) return;

            int gene1 = point1 / mutablePerGene;
            int pos1 = point1 % mutablePerGene + headerFreeze;

            int gene2 = point2 / mutablePerGene;
            int pos2 = point2 % mutablePerGene + headerFreeze;

            int currentGene = gene1;
            int currentPos = pos1;

            while (currentGene < gene2 || (currentGene == gene2 && currentPos < pos2))
            {
                int temp = chromosome1[currentGene][currentPos];
                chromosome1[currentGene][currentPos] = chromosome2[currentGene][currentPos];
                chromosome2[currentGene][currentPos] = temp;

                currentPos++;
                if (currentPos >= geneLength)
                {
                    currentPos = 0;
                    currentGene++;
                }
            }
        }

        public static void GeneCrossover(int[][] chromosome1, int[][] chromosome2, int headerFreeze, Random rng)
        {
            int numGenes = chromosome1.Length;
            if (numGenes <= 1) return;

            int geneIndex = rng.Next(numGenes);

            for (int i = 0; i < chromosome1[geneIndex].Length; i++)
            {
                int temp = chromosome1[geneIndex][i];
                chromosome1[geneIndex][i] = chromosome2[geneIndex][i];
                chromosome2[geneIndex][i] = temp;
            }
        }

        // ============================================================
        // ====================== MUTATION ============================
        // ============================================================

        public static void Mutation(int[][] chromosome, int headerFreeze, int headLength, double mutationRate,
                                    IReadOnlyList<int> functions, IReadOnlyList<int> terminals, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes == 0) return;

            int geneLength = chromosome[0].Length;
            int mutableHeadStart = headerFreeze;
            int tailStart = headLength;

            for (int g = 0; g < numGenes; g++)
            {
                int[] gene = chromosome[g];

                for (int i = mutableHeadStart; i < tailStart && i < geneLength; i++)
                {
                    if (rng.NextDouble() < mutationRate)
                    {
                        if (rng.NextDouble() < 0.5)
                        {
                            gene[i] = functions[rng.Next(functions.Count)];
                        }
                        else
                        {
                            gene[i] = terminals[rng.Next(terminals.Count)];
                        }
                    }
                }

                for (int i = tailStart; i < geneLength; i++)
                {
                    if (rng.NextDouble() < mutationRate)
                    {
                        if (rng.NextDouble() < 0.5)
                        {
                            gene[i] = terminals[rng.Next(terminals.Count)];
                        }
                    }
                }
            }
        }

        public static void MutationConstants(double[][] chromosome, double constantMutationRate, double constantMin,
                                     double constantMax, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes == 0) return;
            int geneLength = chromosome[0].Length;
            for (int g = 0; g < numGenes; g++)
            {
                double[] gene = chromosome[g];
                for (int i = 0; i < geneLength; i++)
                {
                    if (rng.NextDouble() < constantMutationRate)
                    {
                        // Small perturbation or full random
                        if (rng.NextDouble() < 0.5)
                        {
                            gene[i] += (rng.NextDouble() - 0.5) * 2.0; // small change
                        }
                        else
                        {
                            gene[i] = constantMin + rng.NextDouble() * (constantMax - constantMin);
                        }
                    }
                }
            }
        }



        // ============================================================
        // ==================== TRANSPOSITION =========================
        // ============================================================

        public static void ISTransposition(int[][] chromosome, int headerFreeze, int headLength,
                                   int maxTransposonLength, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes == 0) return;

            int geneLength = chromosome[0].Length;
            int mutableHeadStart = headerFreeze;
            int mutableHeadLength = headLength - headerFreeze;

            if (mutableHeadLength <= 1) return;

            int geneIndex = rng.Next(numGenes);
            int[] gene = chromosome[geneIndex];

            int transposonLen = rng.Next(1, maxTransposonLength + 1);
            if (transposonLen >= mutableHeadLength) transposonLen = mutableHeadLength - 1;

            // === FIX: Ensure insertion stays entirely within the head ===
            int maxInsertStart = headLength - transposonLen;
            int insertPoint = mutableHeadStart + rng.Next(1, (maxInsertStart - mutableHeadStart) + 1);

            // === Allocation-free using ArrayPool ===
            var pool = ArrayPool<int>.Shared;
            int[] transposon = pool.Rent(transposonLen);

            try
            {
                int sourceGene = rng.Next(numGenes);
                int sourcePos = rng.Next(geneLength - transposonLen + 1);

                for (int i = 0; i < transposonLen; i++)
                {
                    transposon[i] = chromosome[sourceGene][sourcePos + i];
                }

                // Shift only within the head (never touch the tail)
                for (int i = headLength - 1; i >= insertPoint + transposonLen; i--)
                {
                    gene[i] = gene[i - transposonLen];
                }

                // Insert the transposon
                for (int i = 0; i < transposonLen; i++)
                {
                    gene[insertPoint + i] = transposon[i];
                }
            }
            finally
            {
                pool.Return(transposon);
            }
        }

        public static void RISTransposition(int[][] chromosome, int headerFreeze, int headLength,
                                            int maxTransposonLength, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes == 0) return;

            int geneLength = chromosome[0].Length;
            int mutableHeadStart = headerFreeze;
            int mutableHeadLength = headLength - headerFreeze;

            if (mutableHeadLength < 2) return;

            int geneIndex = rng.Next(numGenes);
            int[] gene = chromosome[geneIndex];

            int transposonLen = rng.Next(1, maxTransposonLength + 1);
            if (transposonLen > mutableHeadLength) transposonLen = mutableHeadLength;

            int insertPoint = mutableHeadStart;

            // === FIX: Only shift inside the head (never touch or pollute the tail) ===
            for (int i = headLength - 1; i >= insertPoint + transposonLen; i--)
            {
                gene[i] = gene[i - transposonLen];
            }

            // Pick source from within the mutable head
            int start = mutableHeadStart + rng.Next(mutableHeadLength - transposonLen + 1);

            for (int i = 0; i < transposonLen; i++)
            {
                gene[insertPoint + i] = gene[start + i];
            }
        }
        public static void GeneTransposition(int[][] chromosome, double[][] chromosomeConstants, int headerFreeze, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes <= 1) return;

            int geneIndex = rng.Next(1, numGenes);

            for (int i = 0; i < chromosome[0].Length; i++)
            {
                int temp = chromosome[0][i];
                chromosome[0][i] = chromosome[geneIndex][i];
                chromosome[geneIndex][i] = temp;
            }
            for (int i = 0; i < chromosomeConstants[0].Length; i++)
            {
                double tempConst = chromosomeConstants[0][i];
                chromosomeConstants[0][i] = chromosomeConstants[geneIndex][i];
                chromosomeConstants[geneIndex][i] = tempConst;
            }
        }

        // ============================================================
        // ====================== INVERSION ===========================
        // ============================================================

        public static void Inversion(int[][] chromosome, int headerFreeze, int headLength, Random rng)
        {
            int numGenes = chromosome.Length;
            if (numGenes == 0) { return; }

            int geneLength = chromosome[0].Length;
            int mutableHeadStart = headerFreeze;
            int mutableHeadEnd = headLength;

            if (mutableHeadEnd - mutableHeadStart < 2) { return; }

            int geneIndex = rng.Next(numGenes);
            int[] gene = chromosome[geneIndex];

            int start = mutableHeadStart + rng.Next(mutableHeadEnd - mutableHeadStart - 1);
            int len = rng.Next(2, mutableHeadEnd - start + 1);

            int end = start + len - 1;
            while (start < end)
            {
                int temp = gene[start];
                gene[start] = gene[end];
                gene[end] = temp;
                start++;
                end--;
            }
        }
    }
}