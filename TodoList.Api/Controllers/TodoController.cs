using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodoList.Api.Data;
using TodoList.Api.DTOs;
using TodoList.Api.Exceptions;
using TodoList.Api.Models;

namespace TodoList.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/todos")]
    public class TodoController : ControllerBase
    {
        private readonly TodoDbContext _context;

        public TodoController(TodoDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        [ProducesResponseType(typeof(TodoResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateTodo([FromBody] CreateTodoRequest request)
        {
            var userId = GetCurrentUserId();

            var todo = new Todo
            {
                Id = Guid.NewGuid(),
                OwnerId = userId,
                Title = request.Title,
                Description = request.Description,
                Status = TodoStatus.Pending, // Default Status is pending
                Priority = request.Priority,
                DueDate = request.DueDate?.ToUniversalTime(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();

            var response = MapToResponse(todo);

            // Returns 201 Created with Location header pointing to the new resource URL
            return Created($"/api/todos/{todo.Id}", response);
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedTodoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> ListTodos(
            [FromQuery] TodoStatus? status,
            [FromQuery] TodoPriority? priority,
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string sortBy = "createdAt",
            [FromQuery] string sortDirection = "desc")
        {
            // Query parameters validation
            if (page < 1)
            {
                ModelState.AddModelError(nameof(page), "Page must be greater than or equal to 1.");
            }
            if (pageSize < 1 || pageSize > 100)
            {
                ModelState.AddModelError(nameof(pageSize), "PageSize must be between 1 and 100.");
            }

            var allowedSortFields = new[] { "createdat", "updatedat", "duedate", "title", "priority" };
            if (!allowedSortFields.Contains(sortBy.ToLowerInvariant()))
            {
                ModelState.AddModelError(nameof(sortBy), $"SortBy must be one of: {string.Join(", ", allowedSortFields)}");
            }

            var allowedDirections = new[] { "asc", "desc" };
            if (!allowedDirections.Contains(sortDirection.ToLowerInvariant()))
            {
                ModelState.AddModelError(nameof(sortDirection), "SortDirection must be either 'asc' or 'desc'.");
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }

            var userId = GetCurrentUserId();
            var query = _context.Todos.Where(t => t.OwnerId == userId);

            // Filtering
            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (priority.HasValue)
            {
                query = query.Where(t => t.Priority == priority.Value);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var keywordLower = keyword.Trim().ToLowerInvariant();
                query = query.Where(t => t.Title.ToLower().Contains(keywordLower));
            }

            // Sorting
            var isAsc = sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy.ToLowerInvariant(), isAsc) switch
            {
                ("createdat", true) => query.OrderBy(t => t.CreatedAt),
                ("createdat", false) => query.OrderByDescending(t => t.CreatedAt),
                ("updatedat", true) => query.OrderBy(t => t.UpdatedAt),
                ("updatedat", false) => query.OrderByDescending(t => t.UpdatedAt),
                ("duedate", true) => query.OrderBy(t => t.DueDate),
                ("duedate", false) => query.OrderByDescending(t => t.DueDate),
                ("title", true) => query.OrderBy(t => t.Title),
                ("title", false) => query.OrderByDescending(t => t.Title),
                ("priority", true) => query.OrderBy(t => t.Priority == TodoPriority.Low ? 1 : t.Priority == TodoPriority.Medium ? 2 : 3),
                ("priority", false) => query.OrderByDescending(t => t.Priority == TodoPriority.Low ? 1 : t.Priority == TodoPriority.Medium ? 2 : 3),
                _ => query.OrderByDescending(t => t.CreatedAt)
            };

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => MapToResponse(t))
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var response = new PagedTodoResponse
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            };

            return Ok(response);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(TodoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTodoById(Guid id)
        {
            var userId = GetCurrentUserId();
            var todo = await _context.Todos.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
            if (todo == null)
            {
                throw new NotFoundException("Todo not found.");
            }

            return Ok(MapToResponse(todo));
        }

        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(TodoResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateTodo(Guid id, [FromBody] UpdateTodoRequest request)
        {
            var userId = GetCurrentUserId();
            var todo = await _context.Todos.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
            if (todo == null)
            {
                throw new NotFoundException("Todo not found.");
            }

            // Apply updates for tracked properties
            if (request.IsPresent(nameof(request.Title)))
            {
                todo.Title = request.Title!;
            }

            if (request.IsPresent(nameof(request.Description)))
            {
                todo.Description = request.Description;
            }

            if (request.IsPresent(nameof(request.Status)))
            {
                todo.Status = request.Status!.Value;
            }

            if (request.IsPresent(nameof(request.Priority)))
            {
                todo.Priority = request.Priority!.Value;
            }

            if (request.IsPresent(nameof(request.DueDate)))
            {
                todo.DueDate = request.DueDate?.ToUniversalTime();
            }

            todo.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(MapToResponse(todo));
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteTodo(Guid id)
        {
            var userId = GetCurrentUserId();
            var todo = await _context.Todos.FirstOrDefaultAsync(t => t.Id == id && t.OwnerId == userId);
            if (todo == null)
            {
                throw new NotFoundException("Todo not found.");
            }

            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        //用識別碼取得目前使用者的ID，若無法取得則拋出未授權例外  
        private Guid GetCurrentUserId()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                throw new UnauthorizedException("User is not authenticated or user ID is invalid.");
            }

            return userId;
        }

        private static TodoResponse MapToResponse(Todo todo)
        {
            return new TodoResponse
            {
                Id = todo.Id,
                Title = todo.Title,
                Description = todo.Description,
                Status = todo.Status,
                Priority = todo.Priority,
                DueDate = todo.DueDate,
                CreatedAt = todo.CreatedAt,
                UpdatedAt = todo.UpdatedAt
            };
        }
    }
}
