namespace CmdNext.Service.Contracts
{
    public interface ICryptoHelper
    {
        string Encrypt(string plainText);

        string Decrypt(string encryptedText);

        string GenerateSalt();

        string HashPassword(string password, string salt);

        bool VerifyPassword(string password, string hash, string salt);
    }
}
