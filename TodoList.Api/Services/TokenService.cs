using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TodoList.Api.Models;

namespace TodoList.Api.Services
{
    public interface ITokenService
    {
        (string Token, DateTime ExpiresAt) GenerateToken(User user);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public (string Token, DateTime ExpiresAt) GenerateToken(User user)
        {
            var secret = _configuration["JWT_SECRET"] ?? _configuration["Jwt:Secret"];
            if (string.IsNullOrEmpty(secret))
            {
                throw new InvalidOperationException("JWT Secret is not configured. Please set the JWT_SECRET environment variable.");
            }

            var issuer = _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? "TodoListApi";
            var audience = _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? "TodoListApi";
            
            var expiryMinutesStr = _configuration["Jwt:ExpiryMinutes"] ?? "30";
            if (!double.TryParse(expiryMinutesStr, out var expiryMinutes))
            {
                expiryMinutes = 30;
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
        }
    }
}
