using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TodoList.Api.Models;

namespace TodoList.Api.DTOs
{
    public class CreateTodoRequest : IValidatableObject
    {
        private string _title = string.Empty;

        [Required]
        public string Title
        {
            get => _title;
            set => _title = value?.Trim() ?? string.Empty;
        }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required]
        public TodoPriority Priority { get; set; }

        public DateTime? DueDate { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title) || Title.Length < 1 || Title.Length > 100)
            {
                yield return new ValidationResult("Title must be between 1 and 100 characters.", new[] { nameof(Title) });
            }
        }
    }

    public class UpdateTodoRequest : IValidatableObject
    {
        private readonly HashSet<string> _presentProperties = new(StringComparer.OrdinalIgnoreCase);

        private string? _title;
        private string? _description;
        private TodoStatus? _status;
        private TodoPriority? _priority;
        private DateTime? _dueDate;

        public string? Title
        {
            get => _title;
            set
            {
                _title = value?.Trim();
                _presentProperties.Add(nameof(Title));
            }
        }

        public string? Description
        {
            get => _description;
            set
            {
                _description = value;
                _presentProperties.Add(nameof(Description));
            }
        }

        public TodoStatus? Status
        {
            get => _status;
            set
            {
                _status = value;
                _presentProperties.Add(nameof(Status));
            }
        }

        public TodoPriority? Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                _presentProperties.Add(nameof(Priority));
            }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                _dueDate = value;
                _presentProperties.Add(nameof(DueDate));
            }
        }

        public bool IsPresent(string propertyName) => _presentProperties.Contains(propertyName);
        public bool HasAnyProperty => _presentProperties.Count > 0;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (IsPresent(nameof(Title)))
            {
                if (string.IsNullOrWhiteSpace(Title) || Title.Length < 1 || Title.Length > 100)
                {
                    yield return new ValidationResult("Title must be between 1 and 100 characters.", new[] { nameof(Title) });
                }
            }

            if (IsPresent(nameof(Description)) && Description != null && Description.Length > 500)
            {
                yield return new ValidationResult("Description cannot exceed 500 characters.", new[] { nameof(Description) });
            }

            if (!HasAnyProperty)
            {
                yield return new ValidationResult("At least one updatable field must be provided.", new[] { "Request" });
            }
        }
    }

    public class TodoResponse
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TodoStatus Status { get; set; }
        public TodoPriority Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class PagedTodoResponse
    {
        public List<TodoResponse> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}
