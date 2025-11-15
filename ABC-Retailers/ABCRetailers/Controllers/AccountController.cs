using ABCRetailers.Models;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ABCRetailers.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ISqlDatabaseService _sqlService; // 1. ADD THIS SERVICE
        private readonly ILogger<AccountController> _logger;

        // 2. UPDATE THE CONSTRUCTOR
        public AccountController(
            IAuthService authService,
            ISqlDatabaseService sqlService, // Add this
            ILogger<AccountController> logger)
        {
            _authService = authService;
            _sqlService = sqlService; // Add this
            _logger = logger;
        }

        #region Login
        // GET: /Account/Login
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // 1. Use the AuthService to validate the user
                var authResult = await _authService.AuthenticateAsync(model.Email, model.Password);

                if (authResult.IsSuccess && authResult.User != null)
                {
                    _logger.LogInformation("User {Email} logged in successfully.", model.Email);

                    // 2. Use the helper to sign the user in
                    await SignInUserAsync(authResult.User, model.RememberMe);

                    // 3. Redirect to the original page or home
                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    AddErrorsToModelState(authResult);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during login for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return View(model);
            }
        }
        #endregion

        #region Register (NEW)
        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterDto());
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterDto model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                // 1. Use the AuthService to register the user
                // This service already handles password hashing and saving to the DB
                var authResult = await _authService.RegisterAsync(model);

                if (authResult.IsSuccess && authResult.User != null)
                {
                    _logger.LogInformation("New user registered: {Email}", model.Email);

                    // 2. Sign in the new user automatically
                    await SignInUserAsync(authResult.User, isPersistent: false);

                    // 3. Redirect to home page
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    // Add errors from the service (e.g., "Email already exists")
                    AddErrorsToModelState(authResult);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during registration for {Email}", model.Email);
                ModelState.AddModelError(string.Empty, "An unexpected error occurred. Please try again.");
                return View(model);
            }
        }
        #endregion

        #region Logout
        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("User {Name} logging out.", User.Identity?.Name);

            // Deletes the authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }
        #endregion


        #region Manage Profile (NEW)

        // GET: /Account/Manage
        [Authorize] // 3. ADD THIS - User must be logged in
        public async Task<IActionResult> Manage()
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Challenge(); // Not logged in or ID not found in cookie
            }

            var user = await _sqlService.GetUserByIdAsync(userId);
            if (user == null)
            {
                // User in cookie doesn't exist in DB (maybe deleted)
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Login");
            }

            // Map the DB model to the View Model
            var viewModel = new ManageProfileViewModel
            {
                Email = user.Email,
                Username = user.Username,
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                DateRegistered = user.DateRegistered,
                LastLogin = user.LastLogin
            };

            return View(viewModel);
        }

        // POST: /Account/Manage
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(ManageProfileViewModel model)
        {
            var userId = GetCurrentUserId();
            if (userId == Guid.Empty)
            {
                return Challenge();
            }

            // We must re-fetch the user from the DB to prevent over-posting
            var userToUpdate = await _sqlService.GetUserByIdAsync(userId);
            if (userToUpdate == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                // Update only the fields we allow
                userToUpdate.FirstName = model.FirstName;
                userToUpdate.LastName = model.LastName;
                userToUpdate.Username = model.Username; // Assumes username can be changed
                userToUpdate.PhoneNumber = model.PhoneNumber;
                userToUpdate.Address = model.Address;

                try
                {
                    await _sqlService.UpdateUserAsync(userToUpdate);
                    TempData["Success"] = "Your profile has been updated successfully!";
                    return RedirectToAction("Manage");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                    ModelState.AddModelError(string.Empty, "An error occurred while saving your profile. Please try again.");
                }
            }

            // If model state is invalid, repopulate read-only fields and return
            model.Email = userToUpdate.Email;
            model.DateRegistered = userToUpdate.DateRegistered;
            model.LastLogin = userToUpdate.LastLogin;
            return View(model);
        }

        #endregion



        #region Helpers
        // REFACTORED: Helper to sign in user
        private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(isPersistent ? 7 : 1),
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        // NEW: Helper to show errors from AuthService
        private void AddErrorsToModelState(AuthResult authResult)
        {
            if (authResult.Errors == null)
            {
                ModelState.AddModelError(string.Empty, "An unknown error occurred.");
                return;
            }

            foreach (var error in authResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }
        }

        // Helper to prevent open redirect attacks
        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }


        private Guid GetCurrentUserId()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(userIdString, out var userId);
            return userId;
        }

        #endregion






    }
}