using BCrypt.Net;
using Microsoft.AspNetCore.Identity;
using Recruit_Finder_AI.Models;

public class BCryptPasswordHasher : IPasswordHasher<ApplicationUser>
{
    public string HashPassword(ApplicationUser user, string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (hashedPassword == null)
        {
            return PasswordVerificationResult.Failed;
        }

        try
        {
            bool isValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
            return isValid ? PasswordVerificationResult.Success : PasswordVerificationResult.Failed;
        }
        catch
        {
            return PasswordVerificationResult.Failed;
        }
    }
}