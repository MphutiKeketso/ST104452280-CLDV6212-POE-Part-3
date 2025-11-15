namespace ABCRetailers.Models
{
    // A standard pattern for returning auth responses
    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string? Token { get; set; }
        public User? User { get; set; }
        public IEnumerable<string>? Errors { get; set; }

        public static AuthResult Success(User user, string token)
        {
            return new AuthResult { IsSuccess = true, User = user, Token = token };
        }

        public static AuthResult Failure(IEnumerable<string> errors)
        {
            return new AuthResult { IsSuccess = false, Errors = errors };
        }

        public static AuthResult Failure(string error)
        {
            return Failure(new[] { error });
        }
    }
}