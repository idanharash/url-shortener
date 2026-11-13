using System.Security.Cryptography;
using UrlShortener.Model.Service;

namespace UrlShortener.BL;

public class CodeGeneratorService : ICodeGeneratorService
{
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    public const int CodeLength = 5;

    // Cryptographically random and unbiased (unlike `byte % 62`), so codes cannot be guessed in sequence.
    public string GenerateCode() => RandomNumberGenerator.GetString(Alphabet, CodeLength);

    /// <summary>Lets lookups reject impossible codes (e.g. /favicon.ico) without touching Redis or the database.</summary>
    public static bool IsValidFormat(string code) => code.Length == CodeLength && code.All(char.IsAsciiLetterOrDigit);
}
