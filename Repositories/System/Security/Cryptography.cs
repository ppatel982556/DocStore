namespace System.Security.Cryptography
{
public class Cryptography()
{
    public static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))
            .Replace("/", "_")
            .Replace("+", "-")
            .Replace("=", "");
    }
}
}