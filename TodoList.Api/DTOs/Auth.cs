using System;
using System.ComponentModel.DataAnnotations;

namespace TodoList.Api.DTOs
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        [MaxLength(100)]
        public string DisplayName { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        [MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(1)]
        [MaxLength(128)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAt { get; set; }
    }

    public class UserResponse
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
