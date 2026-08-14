using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TodoList.Api.Data;
using TodoList.Api.DTOs;

namespace TodoList.Api.Controllers
{
    [ApiController]
    [Route("health")]
    public class HealthController : ControllerBase
    {
        private readonly TodoDbContext _context;

        public HealthController(TodoDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    return Problem(
                        statusCode: StatusCodes.Status503ServiceUnavailable,
                        title: "Service Unavailable",
                        detail: "Database connection failed."
                    );
                }

                return Ok(new HealthResponse { Status = "healthy" });
            }
            catch (Exception ex)
            {
                return Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Service Unavailable",
                    detail: $"Database connection failed: {ex.Message}"
                );
            }
        }
    }

    public class HealthResponse
    {
        public string Status { get; set; } = "healthy";
    }
}
