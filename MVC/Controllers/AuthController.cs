using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Repositories.Constants.Enums;
using Repositories.Interfaces;
using Repositories.Models.ViewModels.Auth;
using Repositories.Services.EmailService;
using Services.AuthService;

namespace MVC.Controllers
{
    [Route("auth")]
    public class AuthController : Controller
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly IPasswordResetInterface _passwordRepository;
        private readonly IAuthInterface _authRepo;

        public AuthController(
            ILogger<AuthController> logger,
            IAuthService authService,
            IAuthInterface authRepo,
            IPasswordResetInterface passwordRepository,
            IEmailService emailService)
        {
            _logger = logger;
            _authService = authService;
            _emailService = emailService;
            _authRepo = authRepo;
            _passwordRepository = passwordRepository;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Dashboard");

            }
            return View();
        }



        [HttpPost("login")]
        public async Task<IActionResult> Login(vmLogin model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        message = "Please enter valid login credentials."
                    });
                }

                var result = await _authService.LoginAsync(model);

                switch (result.Status)
                {
                    case LoginStatus.UserNotFound:
                    case LoginStatus.InvalidPassword:

                        return Unauthorized(new
                        {
                            message = "Invalid email or password."
                        });

                    case LoginStatus.InactiveUser:

                        return Unauthorized(new
                        {
                            message = "Your account has been deactivated. Please contact your administrator."
                        });
                }

                var user = result.User!;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                    new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                foreach (var role in user.Roles)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }

                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,

                        ExpiresUtc = model.RememberMe
                            ? DateTimeOffset.UtcNow.AddDays(30)
                            : DateTimeOffset.UtcNow.AddHours(8),

                        AllowRefresh = true
                    });

                _logger.LogInformation(
                    "User {UserId} logged in successfully.",
                    user.UserId);

                return Ok(new
                {
                    success = true,
                    message = "Login successful.",
                    redirectUrl = Url.Action("Index", "Dashboard")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while logging in.");

                return StatusCode(500, new
                {
                    success = false,
                    message = "An unexpected error occurred."
                });
            }
        }

[HttpGet("logout")]
// [ValidateAntiForgeryToken]
public async Task<IActionResult> Logout()
{
    HttpContext.Session.Clear();

    await HttpContext.SignOutAsync(
        CookieAuthenticationDefaults.AuthenticationScheme);

    return RedirectToAction("Index", "Home");
}

        [HttpGet("ForgotPassword")]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost("ForgotPassword")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVm model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Please enter a valid email address."
                });
            }

            var user = await _authRepo.GetUserByEmailAsync(model.Email);

            const string forgotPasswordResponseMessage = "If the email exists, a reset link has been sent.";

            // Don't reveal whether the email exists
            if (user == null)
            {
                return Ok(new
                {
                    success = true,
                    message = forgotPasswordResponseMessage
                });
            }

            var token = Cryptography.GenerateToken();

            await _passwordRepository.SaveToken(
                user.UserId,
                token,
                DateTime.UtcNow.AddMinutes(30));

            var link = Url.Action(
                nameof(ResetPassword),
                "Auth",
                new { token },
                Request.Scheme);

            await _emailService.SendResetPasswordEmail(
                user.Email,
                link!);

            return Ok(new
            {
                success = true,
                message = forgotPasswordResponseMessage
            });
        }

        [HttpGet("reset-password")]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid reset link.";
                return RedirectToAction(nameof(Login));
            }

            var reset = await _passwordRepository.GetToken(token);
            var tokenError = GetResetTokenError(reset);

            if (tokenError != null)
            {
                TempData["Error"] = tokenError;
                return RedirectToAction(nameof(Login));
            }

            return View(new ResetPasswordVm
            {
                Token = token
            });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordVm model)
        {
            if (!ModelState.IsValid)
            {
                var firstError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                return BadRequest(new
                {
                    success = false,
                    message = firstError ?? "Please enter a valid password."
                });
            }

            var reset = await _passwordRepository.GetToken(model.Token);
            var tokenError = GetResetTokenError(reset);

            if (tokenError != null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = tokenError
                });
            }

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

            await _passwordRepository.UpdatePassword(reset!.UserId, passwordHash);
            await _passwordRepository.MarkUsed(reset.Id);

            TempData["Success"] = "Password has been reset successfully.";

            return Ok(new
            {
                success = true,
                message = "Password has been reset successfully."
            });
        }

        private static string? GetResetTokenError(ResetPasswordVm? reset)
        {
            if (reset == null)
                return "Invalid reset link.";

            if (reset.IsUsed)
                return "This reset link has already been used.";

            if (reset.Expiry <= DateTime.UtcNow)
                return "This reset link has expired.";

            return null;
        }



        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View("Error!");
        }
        [HttpGet("claims")]
        public IActionResult Claims()
        {
            var claims = User.Claims.Select(c => new
            {
                c.Type,
                c.Value
            });

            return Json(claims);
        }
    }
}
