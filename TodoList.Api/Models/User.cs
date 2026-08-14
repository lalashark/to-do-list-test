using System;
using System.Collections.Generic;

namespace TodoList.Api.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public ICollection<Todo> Todos { get; set; } = new List<Todo>();
    }
}
