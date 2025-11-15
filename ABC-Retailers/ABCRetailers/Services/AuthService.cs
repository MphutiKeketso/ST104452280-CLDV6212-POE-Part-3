using ABCRetailers.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration; // For JWT settings
using Microsoft.IdentityModel.Tokens;     // For JWT
using System;
using System.Collections.Generic;        // For List/IEnumerable
using System.IdentityModel.Tokens.Jwt;   // For JWT
using System.Security.Claims;            // For JWT
using System.Security.Cryptography;      // For password hashing
using System.Text;                       // For JWT key
using System.Threading.Tasks;            // For Task

namespace ABCRetailers.Services
{
    public class AuthService : IAuthService
    {
        private readonly ISqlDatabaseService _sqlService;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _config; // ADDED: To read JWT settings

        // Hashing constants
        private const int Iterations = 600000;
        private const int HashSize = 32;
        private const int SaltSize = 16;

        // UPDATED CONSTRUCTOR: Inject IConfiguration
        public AuthService(ISqlDatabaseService sqlService, ILogger<AuthService> logger, IConfiguration config)
        {
            _sqlService = sqlService;
            _logger = logger;
            _config = config;
        }

        // UPDATED: Now returns AuthResult
        public async Task<AuthResult> AuthenticateAsync(string email, string password)
        {
            try
            {
                var user = await _sqlService.GetUserByEmailAsync(email);

                if (user == null)
                {
                    _logger.LogWarning($"Authentication failed: User not found - {email}");
                    return AuthResult.Failure("Invalid email or password.");
                }

                if (!user.IsActive)
                {
                    _logger.LogWarning($"Authentication failed: Account inactive - {email}");
                    return AuthResult.Failure("Account is inactive.");
                }

                if (!VerifyPassword(password, user.PasswordHash, user.Salt))
                {
                    _logger.LogWarning($"Authentication failed: Invalid password - {email}");
                    return AuthResult.Failure("Invalid email or password.");
                }

                await _sqlService.UpdateLastLoginAsync(user.UserId);
                _logger.LogInformation($"User authenticated successfully - {email}");

                // Generate token
                var token = GenerateJwtToken(user);

                return AuthResult.Success(user, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred during authentication for {email}");
                return AuthResult.Failure("An unexpected error occurred. Please try again.");
            }
        }

        // NEW: Implementation for RegisterAsync
        public async Task<AuthResult> RegisterAsync(RegisterDto model)
        {
            try
            {
                // 1. Validate input
                if (model.Password.Length < 8)
                {
                    return AuthResult.Failure("Password must be at least 8 characters long.");
                }

                // 2. Check for existing users
                if (await _sqlService.EmailExistsAsync(model.Email))
                {
                    return AuthResult.Failure("An account with this email already exists.");
                }
                if (await _sqlService.UsernameExistsAsync(model.Username))
                {
                    return AuthResult.Failure("An account with this username already exists.");
                }

                // 3. Hash password
                var (hash, salt) = CreatePasswordHash(model.Password);

                // 4. Create User object
                var user = new User
                {
                    // UserId, CreatedAt, etc. will be set by SqlDatabaseService
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = hash,
                    Salt = salt,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    IsActive = true, // Activate user immediately
                    Role = "Customer" // Default role
                };

                // 5. Save to database
                var createdUser = await _sqlService.CreateUserAsync(user);

                _logger.LogInformation("New user registered: {Email}", createdUser.Email);

                // 6. Generate token and return success
                var token = GenerateJwtToken(createdUser);
                return AuthResult.Success(createdUser, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during user registration for {Email}", model.Email);
                return AuthResult.Failure("An unexpected error occurred. Please try again.");
            }
        }

        // NEW: Implementation for GenerateJwtToken
        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            // Get secret key from appsettings.json
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]
                ?? throw new InvalidOperationException("JWT Key not found in configuration"));

            // Define token claims
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()), // Subject (user ID)
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique Token ID
                new Claim(ClaimTypes.Role, user.Role) // User role
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8), // Token lifetime
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        #region Password Hashing Methods

        private (string hash, string salt) CreatePasswordHash(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);

            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                saltBytes,
                Iterations,
                HashAlgorithmName.SHA256))
            {
                var hashBytes = pbkdf2.GetBytes(HashSize);

                var saltBase64 = Convert.ToBase64String(saltBytes);
                var hashBase64 = Convert.ToBase64String(hashBytes);

                return (hashBase64, saltBase64);
            }
        }

        private bool VerifyPassword(string enteredPassword, string storedHash, string storedSalt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(storedSalt);
                var hashBytes = Convert.FromBase64String(storedHash);

                using (var pbkdf2 = new Rfc2898DeriveBytes(
                    enteredPassword,
                    saltBytes,
                    Iterations,
                    HashAlgorithmName.SHA256))
                {
                    var testHash = pbkdf2.GetBytes(HashSize);
                    return CryptographicOperations.FixedTimeEquals(testHash, hashBytes);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during password verification.");
                return false;
            }
        }

        #endregion
    }
}