using System;

namespace TodoList.Api.Models
{
    public class Todo
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public User Owner { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TodoStatus Status { get; set; } = TodoStatus.Pending;
        public TodoPriority Priority { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
