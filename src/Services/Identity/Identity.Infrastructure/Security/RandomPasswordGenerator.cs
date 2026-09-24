using System.Security.Cryptography;
using System.Text;

/// Generates the one-time password handed to a new employee.
///
/// Uses a cryptographic RNG and guarantees at least one character from each required class, then
/// shuffles — so the result always satisfies the policy without the caller retrying.
public sealed class RandomPasswordGenerator : IPasswordGenerator
{
  // no I, l, 1, O, 0 — these get misread when a password is written down or read aloud
  private const string Uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
  private const string Lowercase = "abcdefghijkmnopqrstuvwxyz";
  private const string Digits = "23456789";
  private const string Symbols = "!@#$%^&*?-_";

  public string Generate(PasswordPolicyOptions policy)
  {
    ArgumentNullException.ThrowIfNull(policy);

    var alphabet = new StringBuilder(Uppercase + Lowercase + Digits);
    var required = new List<char>
    {
      Pick(Uppercase),
      Pick(Lowercase),
      Pick(Digits)
    };

    if (policy.RequireNonAlphanumeric)
    {
      alphabet.Append(Symbols);
      required.Add(Pick(Symbols));
    }

    // comfortably above the policy minimum: a generated password is never typed from memory
    var length = Math.Max(policy.MinimumLength, 14);
    var pool = alphabet.ToString();

    var characters = new List<char>(required);
    while (characters.Count < length)
      characters.Add(Pick(pool));

    return new string(Shuffle(characters));
  }

  private static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];

  private static char[] Shuffle(List<char> characters)
  {
    var array = characters.ToArray();

    for (var i = array.Length - 1; i > 0; i--)
    {
      var j = RandomNumberGenerator.GetInt32(i + 1);
      (array[i], array[j]) = (array[j], array[i]);
    }

    return array;
  }
}
