using System.Security.Cryptography;
using UrlShortener.Model.Service;

namespace UrlShortener.BL
{
    public class CodeGeneratorService: ICodeGeneratorService
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const int codeLength = 5;
        public string GenerateCode()
        {
            var bytes = new byte[codeLength];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);

            var code = new char[codeLength];
            for (int i = 0; i < codeLength; i++)
                code[i] = chars[bytes[i] % chars.Length];

            return new string(code);
        }
    }
}
