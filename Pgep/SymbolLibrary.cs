using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pgep
{
    public enum SymbolType
    {
        Function,
        Variable,
        Constant
    }
    public interface ISymbol
    {
        SymbolType Type { get; }
        string Name { get; }
        int Arity { get; }
        Func<double[], int, double> Evaluate { get; }
    }
    public class Function : ISymbol
    {
        public Function(string name, int arity, Func<double[], int, double> evaluate)
        {
            _name = name;
            _arity = arity;
            _evaluate = evaluate;
        }
        public string Name { get { return _name; } }
        public int Arity { get { return _arity; } }
        public SymbolType Type { get { return SymbolType.Function; } }
        public Func<double[], int, double> Evaluate { get { return _evaluate; } }
        #region private
        private string _name;
        private int _arity;
        private Func<double[], int, double> _evaluate;
        #endregion
    }
    public class Variable : ISymbol
    {
        public Variable(string name, IReadOnlyList<double> values)
        {
            _name = name;
            _values = values;
            _evaluate = (args, i) => _values[i];
        }
        public string Name { get { return _name; } }
        public int Arity { get { return 0; } }
        public IReadOnlyList<double> Values { get { return _values; } }
        public SymbolType Type { get { return SymbolType.Variable; } }
        public Func<double[], int, double> Evaluate { get { return _evaluate; } }
        #region private
        private string _name;
        private IReadOnlyList<double> _values;
        private Func<double[], int, double> _evaluate;
        #endregion
    }
    public class Constant : ISymbol
    {
        public Constant(string name, int index)
        {
            _name = name;
            _index = index;
            _evaluate = (args, i) => args[_index];
        }
        public string Name { get { return _name; } }
        public int Arity { get { return 0; } }
        public int Index { get { return _index; } }
        public SymbolType Type { get { return SymbolType.Constant; } }
        public Func<double[], int, double> Evaluate { get { return _evaluate; } }
        #region private
        private string _name;
        private int _index;
        private Func<double[], int, double> _evaluate;
        #endregion
    }


    [Flags]
    public enum PredefinedLibrary
    {
        None = 0,
        BasicArithmetic = 1 << 0,
        Trigonometric = 1 << 1,
        Logical = 1 << 2,
        Conditional = 1 << 3,
        Neural = 1 << 4
    }

    public class SymbolLibraryBuilder
    {
        public void AddFunction(string name, int arity, Func<double[], int, double> evaluate)
        {
            _checkName(name);
            _symbolLookup[name] = new Function(name, arity, evaluate);
            if (arity > _maxArity)
            {
                _maxArity = arity;
            }
         }
        public void AddVariable(string name, IReadOnlyList<double> values)
        {
            _checkName(name);
            _checkCaseCount(values.Count);
            _symbolLookup[name] = new Variable(name, values);
        }
        public void AddVariable(string name, List<double> values)
        {
            _checkName(name);
            _checkCaseCount(values.Count);
            _symbolLookup[name] = new Variable(name, values);
         }
        public void AddVariable(string name, params double[] values)
        {
            _checkName(name);
            _checkCaseCount(values.Length);
            _symbolLookup[name] = new Variable(name, values);
        }
        public SymbolLibrary Build()
        {
            return new SymbolLibrary(_symbolLookup, _caseCount, _maxArity, _numberConstants);
        }
        public static SymbolLibraryBuilder Create(ParmsSymbolLibrary parms)
        {
            var symb = new SymbolLibraryBuilder(parms.NumberConstants);
            if (parms.PredefinedLibraries.HasFlag(PredefinedLibrary.BasicArithmetic))
            {
                symb.AddFunction("+", 2, (args, i) => args[0] + args[1]);
                symb.AddFunction("-", 2, (args, i) => args[0] - args[1]);
                symb.AddFunction("*", 2, (args, i) => args[0] * args[1]);
                symb.AddFunction("/", 2, (args, i) => args[0] / args[1]);
                symb.AddFunction("^", 2, (args, i) => Math.Pow(args[0], args[1]));
                symb.AddFunction("neg", 1, (args, i) => -args[0]);  
                symb.AddFunction("sqrt", 1, (args, i) => Math.Sqrt(args[0]));
                symb.AddFunction("abs", 1, (args, i) => Math.Abs(args[0]));
                symb.AddFunction("exp", 1, (args, i) => Math.Exp(args[0]));
                symb.AddFunction("log", 1, (args, i) => Math.Log(args[0]));
                symb.AddFunction("pow", 2, (args, i) => Math.Pow(args[0], args[1]));
            }
            if (parms.PredefinedLibraries.HasFlag(PredefinedLibrary.Trigonometric))
            {
                symb.AddFunction("sin", 1, (args, i) => Math.Sin(args[0]));
                symb.AddFunction("cos", 1, (args, i) => Math.Cos(args[0]));
                symb.AddFunction("tan", 1, (args, i) => Math.Tan(args[0])); 
            }
            if (parms.PredefinedLibraries.HasFlag(PredefinedLibrary.Logical))
            {
                symb.AddFunction("and", 2, (args, i) => args[0] != 0 && args[1] != 0 ? 1.0 : 0.0);
                symb.AddFunction("or", 2, (args, i) => args[0] != 0 || args[1] != 0 ? 1.0 : 0.0);
                symb.AddFunction("not", 1, (args, i) => args[0] == 0 ? 1.0 : 0.0);
            }
            if (parms.PredefinedLibraries.HasFlag(PredefinedLibrary.Conditional))
            {
                symb.AddFunction("gt", 4, (args, i) => args[0] > args[1] ? args[2] : args[3]);
                symb.AddFunction("lt", 4, (args, i) => args[0] < args[1] ? args[2] : args[3]);
            }
            if (parms.PredefinedLibraries.HasFlag(PredefinedLibrary.Neural))
            {
                symb.AddFunction("sigmoid", 1, (args, i) => (1.0 / (1.0 + Math.Exp(-args[0]))) - 1.0);
            }
            return symb;
        }
        #region internal
        internal SymbolLibraryBuilder(int numberConstants)
        {
            _symbolLookup = new Dictionary<string, ISymbol>();
            _createConstants(numberConstants);
            _caseCount = 0;
            _maxArity = 0;
            _numberConstants = numberConstants;
        }
        #endregion
        #region private
        private void _checkName(string name)
        {
            if (_symbolLookup.ContainsKey(name))
            {
                throw new ArgumentException($"Symbol name '{name}' is already defined.");
            }
        }
        private void _checkCaseCount(int caseCount)
        {
            if (_caseCount == 0)
            {
                _caseCount = caseCount;
            }
            if (_caseCount != caseCount)
            {
                throw new Exception("All variables must have the same number of cases (data points).");
            }
        }
        private void _createConstants(int constantCount)
        {
            for (int i = 0; i < constantCount; i++)
            {
                string name = $"_c{i}";
                _symbolLookup[name] = new Constant(name, i);
            }
        }
        private Dictionary<string, ISymbol> _symbolLookup;
        private int _caseCount;
        private int _maxArity;
        private int _numberConstants;
        #endregion
    }

    public class SymbolLibrary
    {
        public ISymbol GetSymbol(string name)
        {
            if (_indexLookup.TryGetValue(name, out var index))
            {
                return _symbols[(int)index];
            }
            else
            {
                throw new ArgumentException($"Symbol '{name}' not found in library.");
            }
        }
        public int CaseCount { get { return _caseCount; } }
        public int MaxArity { get { return _maxArity; } }
        public int NumberConstants { get { return _numberConstants; } }
        public int this[string name] { get { return _indexLookup[name]; } }
        public IReadOnlyList<ISymbol> Symbols { get { return _symbols; } }
        public IReadOnlyList<int> FunctionIndices { get { return _functionIndices; } }
        public IReadOnlyList<int> TerminalIndices { get { return _terminalIndices; } }
        #region internal
        internal SymbolLibrary(Dictionary<string, ISymbol> symbolLookup, int caseCount, int maxArity, int numberConstants)
        {
            _caseCount = caseCount;
            _maxArity = maxArity;
            _numberConstants = numberConstants;
            _symbols = _createList(symbolLookup);
            _indexLookup = _createIndexLookup(_symbols);
            _functionIndices = _createFunctionIndices(_symbols);
            _terminalIndices = _createTerminalIndices(_symbols);
        }
        #endregion
        #region private
        private IReadOnlyList<ISymbol> _createList(Dictionary<string, ISymbol> symbolLookup)
        {
            var funcList = symbolLookup.Values.Where(s => s.Type == SymbolType.Function).ToList();
            var varList = symbolLookup.Values.Where(s => s.Type == SymbolType.Variable).ToList();
            var constList = symbolLookup.Values.Where(s => s.Type == SymbolType.Constant).ToList();
            return funcList.Concat(varList).Concat(constList).ToList().AsReadOnly();
        }
        private IReadOnlyDictionary<string, int> _createIndexLookup(IReadOnlyList<ISymbol> symbols)
        {
            var indexLookup = new Dictionary<string, int>();
            for (int i = 0; i < symbols.Count; i++)
            {
                indexLookup.Add(symbols[i].Name, i);
            }
            return indexLookup.AsReadOnly();
        }
        private IReadOnlyList<int> _createFunctionIndices(IReadOnlyList<ISymbol> symbols)
        {
            var indices = new List<int>();
            for (int i = 0; i < symbols.Count; i++)
            {
                if (symbols[i].Type == SymbolType.Function)
                {
                    indices.Add(i);
                }
            }
            return indices.AsReadOnly();
        }
        private IReadOnlyList<int> _createTerminalIndices(IReadOnlyList<ISymbol> symbols)
        {
            var indices = new List<int>();
            for (int i = 0; i < symbols.Count; i++)
            {
                if (symbols[i].Type != SymbolType.Function)
                {
                    indices.Add(i);
                }
            }
            return indices.AsReadOnly();
        }
        private IReadOnlyDictionary<string, int> _indexLookup;
        private IReadOnlyList<ISymbol> _symbols;
        private IReadOnlyList<int> _functionIndices;
        private IReadOnlyList<int> _terminalIndices;
        private int _caseCount;
        private int _maxArity;
        private int _numberConstants;
        #endregion
    }


}
