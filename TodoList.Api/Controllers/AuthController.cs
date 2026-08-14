using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoList.Api.Data;
using TodoList.Api.DTOs;
using TodoList.Api.Exceptions;
using TodoList.Api.Models;
using TodoList.Api.Services;

namespace TodoList.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly TodoDbContext _context;
        private readonly ITokenService _tokenService;

        public AuthController(TodoDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            // Case-insensitive check
            var emailLower = request.Email.ToLowerInvariant();
            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
            if (exists)
            {
                throw new ConflictException("Email is already registered.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = request.Email,
                DisplayName = request.DisplayName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                if (await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower))
                {
                    throw new ConflictException("Email is already registered.");
                }
                throw;
            }

            var response = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                DisplayName = user.DisplayName,
                CreatedAt = user.CreatedAt
            };

            // Requirement: "Location: 新建立使用者資源的位置（若有提供使用者查詢 endpoint）"
            // Since we don't have a user query endpoint defined in OpenAPI, returning empty string or a placeholder path is fine.
            // Let's specify empty string or /api/auth/profile, or just omit/use empty since no profile endpoint is listed.
            // Actually, we can return CreatedAtAction or just Created(string.Empty, response).
            return Created(string.Empty, response);
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var emailLower = request.Email.ToLowerInvariant();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                // Must return exactly 401 with generic error details to not expose user existence details.
                return Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid credentials",
                    detail: "The email or password is incorrect.",
                    type: "https://example.com/problems/invalid-credentials"
                );
            }
            //去tokenservice 那邊取得通行證
            var (token, expiresAt) = _tokenService.GenerateToken(user);

            var response = new LoginResponse
            {
                AccessToken = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt
            };

            return Ok(response);
        }
    }
}
