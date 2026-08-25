using System;
using System.Collections.Generic;
using System.Text;

namespace Pgep
{
    public class Gene
    {
        public Gene(IReadOnlyList<ISymbol> symbols, int headLength, int maxArity, int numberConstants)
        {
            if (headLength < 1) throw new ArgumentException("Head length must be >= 1.");
            _headLength = headLength;
            _tailLength = _headLength * (maxArity - 1) + 1;
            _sequence = new int[_headLength + _tailLength];
            _constants = new double[numberConstants];

            _symbols = symbols;
            _argBuffer = new double[maxArity];
            _stack = new double[_headLength + _tailLength]; // Maybe we need a +1 here to be safe? Not sure if we can have more args than sequence length, but just in case.
        }
        public int HeadLength { get { return _headLength; } }
        public int TailLength { get { return _tailLength; } }
        public int Length { get { return _sequence.Length; } }
        public int[] Sequence { get { return _sequence; } }
        public double Evaluate(int idx, int sequenceEnd = 0)
        {
            int stackTop = 0;
            sequenceEnd = (sequenceEnd == 0) ? GetSequenceEnd() : sequenceEnd;

            for (int i = sequenceEnd; i >= 0; i--)
            {
                var ele = _sequence[i];
                var arity = _symbols[ele].Arity;
                var symType = _symbols[ele].Type;
                var evaluate = _symbols[ele].Evaluate;

                double result = 0.0;

                switch (symType)
                {
                    case SymbolType.Function:
                        if (stackTop < arity) { throw new ArgumentException("Malformed expression (stack underflow)"); }
                        for (int j = 0; j < arity; j++)
                        {
                            stackTop--;
                            _argBuffer[j] = _stack[stackTop];
                        }
                        result = evaluate(_argBuffer, idx);
                        if (double.IsNaN(result) || double.IsPositiveInfinity(result) || double.IsNegativeInfinity(result))
                        {
                            // Handle invalid math results gracefully by returning 0 (common GEP convention).
                            result = 0.0;
                        }
                        break;
                    case SymbolType.Variable:
                        result = evaluate(_argBuffer, idx);
                        break;
                    case SymbolType.Constant:
                        result = evaluate(_constants, idx);
                        break;
                    default:
                        throw new ArgumentException($"Internal Error - unknown SymbolType {symType.ToString()}");
                }

                _stack[stackTop] = result;
                stackTop++;
            }

            if (stackTop != 1)
            {
                throw new ArgumentException("Malformed expression (invalid result count)");
            }

            return _stack[0];
        }
        public int GetSequenceEnd()
        {
            int sequenceEnd = -1;
            int argCount = 1;
            while (argCount > 0)
            {
                sequenceEnd++;
                argCount = argCount + _symbols[_sequence[sequenceEnd]].Arity - 1;
            }
            return sequenceEnd;
        }
        public void SetSequence(int[] sequence)
        {
            if (sequence.Length != Length)
            {
                throw new ArgumentException($"Sequence must be exactly {Length} elements long.");
            }
            Array.Copy(sequence, _sequence, Length);
        }
        public double[] Constants { get { return _constants; } }
        public void SetConstants(double[] constants)
        {
            if (constants.Length != TailLength)
            {
                throw new InvalidOperationException("Constants must be of length TailLength.");
            }
            if (constants.Length != TailLength)
            {
                throw new ArgumentException($"Constants array must be length {TailLength}.");
            }
            Array.Copy(constants, _constants, TailLength);
        }
        public IReadOnlyList<ISymbol> Symbols { get { return _symbols; } }
        public void CopyTo(Gene target)
        {
            Array.Copy(_sequence, target._sequence, Length);
            Array.Copy(_constants, target._constants, _constants.Length);
        }
        #region private
        private int _headLength;
        private int _tailLength;
        private readonly int[] _sequence;
        private readonly double[] _constants;
        private double[] _argBuffer;
        private double[] _stack;
        private IReadOnlyList<ISymbol> _symbols;
        #endregion
    }
    public class Chromosome
    {
        public Chromosome(IReadOnlyList<ISymbol> symbols, int numGenes, int headLength, int maxArity, int constants, Func<double[], double> linkingFunction)
        {
            if (numGenes < 1) throw new ArgumentException("Must have at least 1 gene.");
            if (headLength < 1) throw new ArgumentException("Head length must be >= 1.");

            _headLength = headLength;
            _maxArity = maxArity;
            _constants = constants;

            _geneSequences = new int[numGenes][];
            _geneConstants = new double[numGenes][];
            _buffer = new double[numGenes];

            _genes = new Gene[numGenes];
            for (int i = 0; i < _genes.Length; i++)
            {
                _genes[i] = new Gene(symbols, _headLength, _maxArity, _constants);
                _geneSequences[i] = _genes[i].Sequence;
                _geneConstants[i] = _genes[i].Constants;
            }
            _linkingFunction = linkingFunction;
        }
        public int GeneCount { get { return _genes.Length; } }
        public int HeadLength { get { return _headLength; } }
        public int MaxArity { get { return _maxArity; } }
        public int MaxConstants { get { return _constants; } }
        public double Fitness { get { return _fitness; } set { _fitness = value; } }
        public Gene GetGene(int index) { return _genes[index]; }
        public void SetGene(int index, Gene gene)
        {
            gene.CopyTo(_genes[index]);
        }
        public void CopyTo(Chromosome target)
        {
            if (GeneCount != target.GeneCount)
            {
                throw new ArgumentException("Target chromosome must have the same number of genes.");
            }
            for (int i = 0; i < GeneCount; i++)
            {
                _genes[i].CopyTo(target._genes[i]);
            }
        }
        public int[][] GeneSequences { get { return _geneSequences; } }
        public double[][] GeneConstants { get { return _geneConstants; } }
        public double Evaluate(int index)
        {
            for (int i = 0; i < _genes.Length; i++)
            {
                var gene = _genes[i];
                _buffer[i] = gene.Evaluate(index, 0);
            }

            _value = _linkingFunction(_buffer);
            return _value;
         }
        public IReadOnlyList<ISymbol> Symbols { get { return _genes[0].Symbols; } }
        #region private
        private Gene[] _genes;
        private int _headLength;
        private int _maxArity;
        private int _constants;
        private double _fitness;
        private double _value;
        private int[][] _geneSequences;
        private double[][] _geneConstants;

        private double[] _buffer;
        private Func<double[], double> _linkingFunction;
        #endregion
    }
    public class ChromosomePool
{
    public ChromosomePool(ParmsChromosome pc, SymbolLibrary syms)
    {
        _available = new Queue<Chromosome>();
        _chromosomePoolSize = pc.ChromosomePoolSize;
        _numGenes = pc.NumberGenes;
        _headLength = pc.HeadLength;
        _maxArity = syms.MaxArity;
        _numberConstants = syms.NumberConstants;
        _syms = syms;
        _linkingFunction = pc.LinkingFunction;
    }
    public int CurrentPoolSize { get { return _available.Count(); } }
    public int ChromosomePoolSize { get { return _chromosomePoolSize; } }
    public int NumGenes { get { return _numGenes; } }
    public int HeadLength { get { return _headLength; } }
    public int MaxArity { get { return _syms.MaxArity; } }
    public int NumberConstants { get { return _syms.NumberConstants; } }
    public SymbolLibrary Symbols { get { return _syms; } }
    public Chromosome Get()
    {
        if (_available.Count > 0)
        {
            return _available.Dequeue();
        }
        else
        {
            return new Chromosome(_syms.Symbols, _numGenes, _headLength, _maxArity, _numberConstants, _linkingFunction);
        }
    }
    public void Return(Chromosome chromosome)
    {
        if (_available.Count < _chromosomePoolSize)
        {
            _available.Enqueue(chromosome);
        }
    }
    #region private
    private int _chromosomePoolSize;
    private int _numGenes;
    private int _headLength;
    private int _maxArity;
    private int _numberConstants;
    private SymbolLibrary _syms;
    private Queue<Chromosome> _available;
    private Func<double[], double> _linkingFunction;
    #endregion
}
}
