using System.Globalization;

/// A small, safe arithmetic language for salary rules with calculation method "formula", e.g.
///   basic_pay * 0.15
///   max(1500, min_basic_pay * 0.45)
///   if(bps >= 17, 5000, 3000)
/// Numbers, variables (see PayFormula.Variables), + - * /, parentheses, comparisons (&lt; &lt;= &gt; &gt;= == !=) and the
/// functions min, max, round, floor, ceil, abs and if. Nothing else can run, so a rule can never reach the system.
public sealed class PayFormula
{
  /// What a formula may refer to. gross_pay exists only for deductions (an earning cannot depend on the total it is part of).
  public static readonly IReadOnlyList<string> Variables =
    ["basic_pay", "min_basic_pay", "max_basic_pay", "gross_pay", "bps", "stage", "days_payable", "days_in_period"];

  private readonly Node _root;

  public string Expression { get; }

  private PayFormula(string expression, Node root)
  {
    Expression = expression;
    _root = root;
  }

  public static PayFormula Parse(string? expression)
  {
    if (string.IsNullOrWhiteSpace(expression))
      throw new DomainException("A formula is required.");

    if (expression.Length > 2000)
      throw new DomainException("A formula cannot exceed 2000 characters.");

    var parser = new Parser(expression);
    var root = parser.ParseExpression();
    parser.ExpectEnd();
    return new PayFormula(expression.Trim(), root);
  }

  public IReadOnlySet<string> UsedVariables
  {
    get
    {
      var used = new HashSet<string>(StringComparer.Ordinal);
      _root.CollectVariables(used);
      return used;
    }
  }

  public decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
  {
    try
    {
      return _root.Evaluate(variables);
    }
    catch (DivideByZeroException)
    {
      throw new DomainException($"The formula '{Expression}' divides by zero.");
    }
    catch (OverflowException)
    {
      throw new DomainException($"The formula '{Expression}' gives a number too large to pay.");
    }
  }

  private abstract class Node
  {
    public abstract decimal Evaluate(IReadOnlyDictionary<string, decimal> variables);
    public virtual void CollectVariables(HashSet<string> used) { }
  }

  private sealed class NumberNode(decimal value) : Node
  {
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => value;
  }

  private sealed class VariableNode(string name) : Node
  {
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) =>
        variables.TryGetValue(name, out var value) ? value : throw new DomainException($"The formula uses '{name}', which is not available here.");

    public override void CollectVariables(HashSet<string> used) => used.Add(name);
  }

  private sealed class UnaryNode(Node operand) : Node
  {
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables) => -operand.Evaluate(variables);
    public override void CollectVariables(HashSet<string> used) => operand.CollectVariables(used);
  }

  private sealed class BinaryNode(string op, Node left, Node right) : Node
  {
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
    {
      var a = left.Evaluate(variables);
      var b = right.Evaluate(variables);
      return op switch
      {
        "+" => a + b,
        "-" => a - b,
        "*" => a * b,
        "/" => b == 0 ? throw new DivideByZeroException() : a / b,
        "<" => a < b ? 1 : 0,
        "<=" => a <= b ? 1 : 0,
        ">" => a > b ? 1 : 0,
        ">=" => a >= b ? 1 : 0,
        "==" => a == b ? 1 : 0,
        "!=" => a != b ? 1 : 0,
        _ => throw new InvalidOperationException(op)
      };
    }

    public override void CollectVariables(HashSet<string> used)
    {
      left.CollectVariables(used);
      right.CollectVariables(used);
    }
  }

  private sealed class CallNode(string name, IReadOnlyList<Node> args) : Node
  {
    public override decimal Evaluate(IReadOnlyDictionary<string, decimal> variables)
    {
      if (name == "if")
        return args[0].Evaluate(variables) != 0 ? args[1].Evaluate(variables) : args[2].Evaluate(variables);

      var values = args.Select(a => a.Evaluate(variables)).ToList();
      return name switch
      {
        "min" => values.Min(),
        "max" => values.Max(),
        "abs" => Math.Abs(values[0]),
        "floor" => Math.Floor(values[0]),
        "ceil" => Math.Ceiling(values[0]),
        "round" => Math.Round(values[0], values.Count > 1 ? (int)values[1] : 0, MidpointRounding.AwayFromZero),
        _ => throw new InvalidOperationException(name)
      };
    }

    public override void CollectVariables(HashSet<string> used)
    {
      foreach (var arg in args)
        arg.CollectVariables(used);
    }
  }

  /// Recursive descent: comparison > additive > multiplicative > unary > primary.
  private sealed class Parser(string text)
  {
    private int _position;

    public Node ParseExpression() => ParseComparison();

    public void ExpectEnd()
    {
      SkipSpaces();
      if (_position < text.Length)
        throw Error($"unexpected '{text[_position]}'");
    }

    private Node ParseComparison()
    {
      var left = ParseAdditive();
      foreach (var op in new[] { "<=", ">=", "==", "!=", "<", ">" })
      {
        if (TryRead(op))
          return new BinaryNode(op, left, ParseAdditive());
      }
      return left;
    }

    private Node ParseAdditive()
    {
      var left = ParseMultiplicative();
      while (true)
      {
        if (TryRead("+")) left = new BinaryNode("+", left, ParseMultiplicative());
        else if (TryRead("-")) left = new BinaryNode("-", left, ParseMultiplicative());
        else return left;
      }
    }

    private Node ParseMultiplicative()
    {
      var left = ParseUnary();
      while (true)
      {
        if (TryRead("*")) left = new BinaryNode("*", left, ParseUnary());
        else if (TryRead("/")) left = new BinaryNode("/", left, ParseUnary());
        else return left;
      }
    }

    private Node ParseUnary() => TryRead("-") ? new UnaryNode(ParseUnary()) : TryRead("+") ? ParseUnary() : ParsePrimary();

    private Node ParsePrimary()
    {
      SkipSpaces();
      if (_position >= text.Length)
        throw Error("the formula ends too early");

      if (TryRead("("))
      {
        var inner = ParseExpression();
        Expect(")");
        return inner;
      }

      var c = text[_position];
      if (char.IsAsciiDigit(c) || c == '.')
      {
        var start = _position;
        while (_position < text.Length && (char.IsAsciiDigit(text[_position]) || text[_position] == '.'))
          _position++;
        var literal = text[start.._position];
        if (!decimal.TryParse(literal, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
          throw Error($"'{literal}' is not a number");
        return new NumberNode(number);
      }

      if (char.IsAsciiLetter(c) || c == '_')
      {
        var start = _position;
        while (_position < text.Length && (char.IsAsciiLetterOrDigit(text[_position]) || text[_position] == '_'))
          _position++;
        var name = text[start.._position].ToLowerInvariant();

        if (TryRead("("))
        {
          var args = new List<Node>();
          if (!TryRead(")"))
          {
            do args.Add(ParseExpression());
            while (TryRead(","));
            Expect(")");
          }
          return Call(name, args);
        }

        if (!Variables.Contains(name))
          throw Error($"'{name}' is not a known value (use {string.Join(", ", Variables)})");
        return new VariableNode(name);
      }

      throw Error($"unexpected '{c}'");
    }

    private Node Call(string name, List<Node> args)
    {
      var (min, max) = name switch
      {
        "min" or "max" => (1, 20),
        "abs" or "floor" or "ceil" => (1, 1),
        "round" => (1, 2),
        "if" => (3, 3),
        _ => throw Error($"'{name}' is not a known function (use min, max, round, floor, ceil, abs, if)")
      };

      if (args.Count < min || args.Count > max)
        throw Error($"{name}() takes {(min == max ? min.ToString() : $"{min} to {max}")} argument(s)");

      return new CallNode(name, args);
    }

    private bool TryRead(string token)
    {
      SkipSpaces();
      if (string.CompareOrdinal(text, _position, token, 0, token.Length) != 0)
        return false;
      _position += token.Length;
      return true;
    }

    private void Expect(string token)
    {
      if (!TryRead(token))
        throw Error($"'{token}' expected");
    }

    private void SkipSpaces()
    {
      while (_position < text.Length && char.IsWhiteSpace(text[_position]))
        _position++;
    }

    private DomainException Error(string problem) =>
        new($"The formula is not valid: {problem} (at character {_position + 1}).");
  }
}
