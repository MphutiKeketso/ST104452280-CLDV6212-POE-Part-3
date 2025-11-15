using ABCRetailers.Models;
using System.Threading.Tasks;

namespace ABCRetailers.Services
{
    public interface IAuthService
    {
        // Changed to return the new AuthResult (with token)
        Task<AuthResult> AuthenticateAsync(string email, string password);

        // NEW: Method for handling user registration
        Task<AuthResult> RegisterAsync(RegisterDto model);

        // NEW: Helper to generate a token (could be private, but useful public)
        string GenerateJwtToken(User user);
    }
}