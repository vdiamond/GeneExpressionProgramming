using System;
using System.Collections.Generic;
using System.Text;

namespace Pgep
{
     public class KarvaDecoder
    {
        /// <summary>
        /// Creates a KarvaDecoder for turning GEP K-expressions back into readable infix/math expressions.
        /// </summary>
        /// <param name="symbols">The symbol table from SymbolLibrary.</param>
        /// <param name="linkOperator">Operator used to join multi-gene chromosomes (default "+" since common GEP linking is summation). Examples: "+", "*", " + " (spaces added automatically).</param>
        /// <param name="constantPrecision">Decimal precision for formatting constant values (default 4). Trailing zeros are trimmed for cleanliness.</param>
        public KarvaDecoder(IReadOnlyList<ISymbol> symbols, string linkOperator = "Link+", int constantPrecision = 4)
        {
            _symbols = symbols;
            _sb = new StringBuilder();
            _linkOperator = linkOperator;
            _constPrecision = Math.Max(0, Math.Min(10, constantPrecision));
        }

        public string Decode(int[] sequence, double[] constants)
        {
            _sb.Clear();
            _index = -1;

            int sequenceEnd = -1;
            int argCount = 1;
            while (argCount > 0)
            {
                sequenceEnd++;
                argCount = argCount + _symbols[sequence[sequenceEnd]].Arity - 1;
            }
            sequenceEnd++;

            _parse(sequence, constants, sequenceEnd, parentPrec: 0, isRight: false);
            return _sb.ToString();
        }

        public string Decode(Gene gene)
        {
            return Decode(gene.Sequence, gene.Constants);
        }

        public string Decode(Chromosome chrom)
        {
            if (chrom.GeneCount == 0) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < chrom.GeneCount; i++)
            {
                var gene = chrom.GetGene(i);
                string g = Decode(gene);
                if (i > 0)
                {
                    sb.Append(' ');
                    sb.Append(_linkOperator);
                    sb.Append(' ');
                }
                if (chrom.GeneCount > 1)
                {
                    // Wrap each gene expression so multi-gene structure is clear and
                    // linking is always unambiguous regardless of link operator precedence.
                    sb.Append('(');
                    sb.Append(g);
                    sb.Append(')');
                }
                else
                {
                    sb.Append(g);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// The operator string used when formatting chromosomes with multiple genes.
        /// Setting this updates how Decode(Chromosome) joins sub-expressions.
        /// </summary>
        public string LinkOperator
        {
            get => _linkOperator;
            set => _linkOperator = string.IsNullOrWhiteSpace(value) ? "+" : value.Trim();
        }

        #region private
        private bool TryGetInfixInfo(string name, out int prec, out bool leftAssoc, out string display)
        {
            prec = 0;
            leftAssoc = true;
            display = name;

            if (string.IsNullOrEmpty(name))
                return false;

            // Support the core arithmetic operators (infix with spaces and proper precedence).
            // Also future-proof for power if users register a function named "^" or "**".
            if (name == "+" || name == "-")
            {
                prec = 1;
                leftAssoc = true;
                return true;
            }
            if (name == "*" || name == "/")
            {
                prec = 2;
                leftAssoc = true;
                return true;
            }
            if (name == "^" || name == "**")
            {
                prec = 3;
                leftAssoc = false; // right-associative
                display = "^";
                return true;
            }
            return false;
        }

        private string FormatConstant(double val)
        {
            if (double.IsNaN(val) || double.IsInfinity(val))
                return val.ToString("G", System.Globalization.CultureInfo.InvariantCulture);

            double rounded = Math.Round(val, _constPrecision);
            if (Math.Abs(rounded - Math.Round(rounded)) < 1e-9)
            {
                // Integer value
                return ((long)Math.Round(rounded)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            string s = rounded.ToString("F" + _constPrecision, System.Globalization.CultureInfo.InvariantCulture);
            // Trim trailing zeros and decimal point for professional look: 1.5000 -> 1.5, 2.0000 -> 2
            if (s.Contains('.'))
            {
                s = s.TrimEnd('0').TrimEnd('.');
            }
            return s;
        }

        private void _parse(int[] sequence, double[] constants, int sequenceEnd, int parentPrec = 0, bool isRight = false)
        {
            _index++;
            if (_index >= sequenceEnd) { return; }

            var ele = sequence[_index];
            var arity = _symbols[ele].Arity;
            var symType = _symbols[ele].Type;
            var name = _symbols[ele].Name;
            var evaluate = _symbols[ele].Evaluate;

            // Compute precedence for this node (used for minimal, correct parentheses)
            int myPrec = 100;
            bool isInfix = false;
            bool leftAssoc = true;
            string dispOp = name;

            if (symType == SymbolType.Function)
            {
                if (TryGetInfixInfo(name, out int p, out bool la, out string d))
                {
                    isInfix = true;
                    myPrec = p;
                    leftAssoc = la;
                    dispOp = d;
                }
                else
                {
                    myPrec = 10; // function call binds tighter than arithmetic
                }
            }
            else if (symType == SymbolType.Variable || symType == SymbolType.Constant)
            {
                myPrec = 100; // atoms / terminals
            }

            // Minimal parentheses decision (standard expr pretty-printing)
            bool wrap = myPrec < parentPrec;
            if (myPrec == parentPrec)
            {
                if (isRight)
                    wrap = leftAssoc;
                else
                    wrap = !leftAssoc;
            }

            if (wrap) _sb.Append('(');

            switch (symType)
            {
                case SymbolType.Function:
                    if (isInfix)
                    {
                        // left child (isRight=false for it)
                        _parse(sequence, constants, sequenceEnd, myPrec, false);
                        _sb.Append(' ');
                        _sb.Append(dispOp);
                        _sb.Append(' ');
                        // right child
                        _parse(sequence, constants, sequenceEnd, myPrec, true);
                    }
                    else
                    {
                        _sb.Append(name);
                        _sb.Append('(');
                        for (int i = 0; i < arity; i++)
                        {
                            if (i > 0) { _sb.Append(", "); }
                            // Args are already grouped by the call's (). Use parentPrec=0 so the
                            // root expr of an arg does not get extra precedence parens (prevents sin((x + 1))).
                            // Children inside the arg will still get correct relative parens.
                            _parse(sequence, constants, sequenceEnd, parentPrec: 0, isRight: false);
                        }
                        _sb.Append(')');
                    }
                    break;
                case SymbolType.Variable:
                    _sb.Append(name);
                    break;
                case SymbolType.Constant:
                    var result = evaluate(constants, 0);
                    _sb.Append(FormatConstant(result));
                    break;
                default:
                    throw new ArgumentException($"Internal Error - unknown SymbolType {symType.ToString()}");
            }

            if (wrap) _sb.Append(')');
        }

        private IReadOnlyList<ISymbol> _symbols;
        private StringBuilder _sb;
        private int _index;
        private string _linkOperator = "+";
        private int _constPrecision = 4;
        #endregion
    }

}
