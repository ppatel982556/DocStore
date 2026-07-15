using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Repositories.Models.ViewModels.Auth
{
    public class ResetPasswordVm
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        public string Token { get; set; } = string.Empty;

        public DateTime Expiry { get; set; }

        public bool IsUsed { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [RegularExpression(
            @"^(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$",
            ErrorMessage = "Password must be at least 8 characters and include at least 1 number and 1 special character.")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare(nameof(Password), ErrorMessage = "Password and confirm password must match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
