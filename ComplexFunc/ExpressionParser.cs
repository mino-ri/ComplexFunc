using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using SyntaxAnalysis;

namespace ComplexFunc
{
    public static class ExpressionParser
    {
        private static bool _initialized;
        private static Lexer _lexer = null!;
        private static PatternParser<Expression> _parser = null!;
        private static readonly Complex E = Math.E;
        private static readonly Complex PI = Math.PI;
        private static readonly ParameterExpression X = Expression.Parameter(typeof(Complex), "x");

        public static Func<Complex, Complex> Parse(string text)
        {
            Initialize();

            var body = _parser.Parse(_lexer.Tokenize(text.ToLower()));
            return Expression.Lambda<Func<Complex, Complex>>(body, X).Compile();
        }

        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _lexer = new Lexer();

            _lexer.AddIgnore(@"\s+");
            var suffix = _lexer.Add("²|³|°", "SUFFIX");
            var prefix = _lexer.Add(@"√|∛|abs|acos|arg|asin|atan|cbrt|cnj|cosh?|deg|exp|im|ln|rad|rcp|re|sinh?|sqrt|tanh?", "PREFIX");
            var prefix2 = _lexer.Add(@"log_|root_", "PREFIX2");
            var infix0 = _lexer.Add(@"\^", "INFIX0");
            var infix1 = _lexer.Add(@"[*/×÷∠]", "INFIX1");
            var infix2 = _lexer.Add(@"[-－+＋]", "INFIX2");
            var number = _lexer.Add(@"[0-9]+([.][0-9]*)?i?|[.][0-9]+i?|i", "NUMBER");
            var open = _lexer.Add(@"\(", "(");
            var close = _lexer.Add(@"\)", ")");
            var variable = _lexer.Add(@"x", "x");
            var constant = _lexer.Add(@"[eπ|pi]", "CONST");

            var expr = Patterns.Choice<Expression>("expr");

            var factor1 = Patterns.Choice(
                number.Map(t => Expression.Constant(ParseComplex(t.Text))),
                constant.Map(t => Expression.Constant(GetConst(t.Text))),
                variable.Map(t => (Expression)X),
                Patterns.Seq(open, expr, close, (_, x, __) => x),
                Patterns.Seq(open, expr, (_, x) => x, ""));

            var factor2 = Patterns.Choice<Expression>("factor");
            factor2.AddRange(
                factor1,
                Patterns.Seq(factor2, suffix, (x, t) => Invoke(GetUnary(t.Text), x), "", Priority.Left(20)),
                Patterns.Seq(factor2, infix0, factor2, (x, t, y) => Invoke(GetBinary(t.Text), x, y), "", Priority.Right(19)));

            var term2 = Patterns.Choice<Expression>("term2");
            var term1 = Patterns.Choice<Expression>("term1");
            term1.AddRange(factor2,
                Patterns.Seq(factor2, term1, (left, right) => Expression.Multiply(left, right), "", Priority.Right(15)),
                Patterns.Seq(prefix, term2, (t, x) => Invoke(GetUnary(t.Text), x), "", Priority.Right(15)),
                Patterns.Seq(prefix, suffix, term2, (t, u, x) => Invoke(GetUnary(u.Text), Invoke(GetUnary(t.Text), x)), "", Priority.Right(15)),
                Patterns.Seq(prefix, infix0, factor2, term2, (t, u, y, x) => Invoke(GetBinary(u.Text), Invoke(GetUnary(t.Text), x), y), "", Priority.Right(15)),
                Patterns.Seq(prefix2, factor2, term2, (t, y, x) => Invoke(GetBinary(t.Text), x, y), "", Priority.Right(15)));

            term2.AddRange(term1,
                Patterns.Seq(infix2, term2, (t, x) => Invoke(GetUnary(t.Text), x), "", Priority.Right(15)));

            expr.AddRange(
                term2,
                Patterns.Seq(expr, infix1, expr, (x, t, y) => Invoke(GetBinary(t.Text), x, y), "", Priority.Left(9)),
                Patterns.Seq(expr, infix2, expr, (x, t, y) => Invoke(GetBinary(t.Text), x, y), "", Priority.Left(8)));

            _parser = expr.CreateParser();
        }

        private static Expression Invoke<TFunc>(TFunc f, params Expression[] args)
        {
            return Expression.Invoke(Expression.Constant(f), args);
        }

        private static Complex ParseComplex(string text)
        {
            if (text == "i")
            {
                return Complex.ImaginaryOne;
            }
            if (text.EndsWith("i"))
            {
                return new Complex(0.0, double.Parse(text.Substring(0, text.Length - 1)));
            }

            return new Complex(double.Parse(text), 0.0);
        }

        private static Func<Complex, Complex> GetUnary(string name)
        {
            return name switch
            {
                "²" => x => x * x,
                "³" => x => x * x * x,
                "°" => x => x * (Math.PI / 180.0),
                "√" => Complex.Sqrt,
                "∛" => x => Complex.Pow(x, 1.0 / 3.0),
                "abs" => x => Complex.Abs(x),
                "acos" => Complex.Acos,
                "arg" => x => x.Phase,
                "asin" => Complex.Asin,
                "atan" => Complex.Atan,
                "cbrt" => x => Complex.Pow(x, 1.0 / 3.0),
                "cnj" => Complex.Conjugate,
                "cos" => Complex.Cos,
                "cosh" => Complex.Cosh,
                "deg" => x => x * (180.0 / Math.PI),
                "exp" => Complex.Exp,
                "im" => x => x.Imaginary,
                "ln" => Complex.Exp,
                "rad" => x => x * (Math.PI / 180.0),
                "rcp" => Complex.Reciprocal,
                "re" => x => x.Real,
                "sin" => Complex.Sin,
                "sinh" => Complex.Sinh,
                "sqrt" => Complex.Sqrt,
                "tan" => Complex.Tan,
                "tanh" => Complex.Tanh,
                "-" => Complex.Negate,
                "－" => Complex.Negate,
                _ => x => x,
            };
        }

        private static Func<Complex, Complex, Complex> GetBinary(string name)
        {
            return name switch
            {
                "^" => Complex.Pow,
                "log_" => (x, y) => Complex.Log(x) / Complex.Log(y),
                "root_" => (x, y) => Complex.Pow(x, 1.0 / y),
                "*" => Complex.Multiply,
                "/" => Complex.Divide,
                "×" => Complex.Multiply,
                "÷" => Complex.Divide,
                "∠" => (x, y) => Complex.FromPolarCoordinates(x.Real, y.Real),
                "-" => Complex.Subtract,
                "－" => Complex.Subtract,
                "+" => Complex.Add,
                "＋" => Complex.Add,
                _ => (x, _) => x,
            };
        }

        private static Complex GetConst(string name)
        {
            return name switch
            {
                "e" => E,
                "π" => PI,
                "pi" => PI,
                _ => Complex.One,
            };
        }
    }
}
