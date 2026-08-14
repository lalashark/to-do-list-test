using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TodoList.Api.DTOs;
using TodoList.Api.Models;
using Xunit;

namespace TodoList.Tests
{
    public class IntegrationTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly CustomWebApplicationFactory<Program> _factory;
        private readonly HttpClient _client;
        private static readonly System.Text.Json.JsonSerializerOptions _options = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) }
        };

        public IntegrationTests(CustomWebApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = _factory.CreateClient();
        }

        [Fact]
        public async Task Register_And_Login_Flow_Success()
        {
            // Arrange
            var email = $"user_{Guid.NewGuid()}@example.com";
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = "Password123!",
                DisplayName = "Test User"
            };

            // Act - Register
            var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest, _options);
            Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

            var registeredUser = await registerResponse.Content.ReadFromJsonAsync<UserResponse>();
            Assert.NotNull(registeredUser);
            Assert.Equal(email, registeredUser.Email);
            Assert.Equal("Test User", registeredUser.DisplayName);
            Assert.NotEqual(Guid.Empty, registeredUser.Id);

            // Act - Login
            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = "Password123!"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest, _options);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(loginResult);
            Assert.NotEmpty(loginResult.AccessToken);
            Assert.Equal("Bearer", loginResult.TokenType);
            Assert.True(loginResult.ExpiresAt > DateTime.UtcNow);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            // Arrange
            var email = $"duplicate_{Guid.NewGuid()}@example.com";
            var registerRequest1 = new RegisterRequest
            {
                Email = email,
                Password = "Password123!",
                DisplayName = "User One"
            };

            // Second register request uses upper case for some characters to test case-insensitive uniqueness
            var registerRequest2 = new RegisterRequest
            {
                Email = email.ToUpperInvariant(),
                Password = "AnotherPassword123!",
                DisplayName = "User Two"
            };

            // Act
            var response1 = await _client.PostAsJsonAsync("/api/auth/register", registerRequest1, _options);
            var response2 = await _client.PostAsJsonAsync("/api/auth/register", registerRequest2, _options);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response1.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);

            var problem = await response2.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal((int)HttpStatusCode.Conflict, problem.Status);
            Assert.NotEmpty(problem.Detail!);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new LoginRequest
            {
                Email = "nonexistent@example.com",
                Password = "WrongPassword!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest, _options);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal("Invalid credentials", problem.Title);
            Assert.Equal("The email or password is incorrect.", problem.Detail);
        }

        [Fact]
        public async Task Todo_Unauthenticated_ReturnsUnauthorized()
        {
            // Act
            var getResponse = await _client.GetAsync("/api/todos");
            var postResponse = await _client.PostAsJsonAsync("/api/todos", new CreateTodoRequest
            {
                Title = "Test Todo",
                Priority = TodoPriority.Medium
            }, _options);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, getResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, postResponse.StatusCode);

            var problem = await getResponse.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Equal("Unauthorized", problem.Title);
        }

        [Fact]
        public async Task Todo_CRUD_And_Isolation_Tests()
        {
            // Arrange - Register & Login User A
            var userAClient = _factory.CreateClient();
            var tokenA = await RegisterAndGetTokenAsync(userAClient, "usera@example.com", "PasswordA123!");
            userAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);

            // Arrange - Register & Login User B
            var userBClient = _factory.CreateClient();
            var tokenB = await RegisterAndGetTokenAsync(userBClient, "userb@example.com", "PasswordB123!");
            userBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);

            // Act - User A creates a Todo
            var createRequest = new CreateTodoRequest
            {
                Title = "User A Todo Title",
                Description = "User A Description",
                Priority = TodoPriority.High,
                DueDate = DateTime.UtcNow.AddDays(2)
            };

            var createResponse = await userAClient.PostAsJsonAsync("/api/todos", createRequest, _options);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var createdTodo = await createResponse.Content.ReadFromJsonAsync<TodoResponse>();
            Assert.NotNull(createdTodo);
            Assert.Equal("User A Todo Title", createdTodo.Title);
            Assert.Equal(TodoStatus.Pending, createdTodo.Status); // Defaults to pending

            var locationHeader = createResponse.Headers.Location?.ToString();
            Assert.Equal($"/api/todos/{createdTodo.Id}", locationHeader);

            // Act - User A retrieves their Todo
            var getByIdResponse = await userAClient.GetAsync($"/api/todos/{createdTodo.Id}");
            Assert.Equal(HttpStatusCode.OK, getByIdResponse.StatusCode);

            // Act - User B lists Todos (should be empty, isolation test)
            var userBListResponse = await userBClient.GetAsync("/api/todos");
            Assert.Equal(HttpStatusCode.OK, userBListResponse.StatusCode);
            var userBList = await userBListResponse.Content.ReadFromJsonAsync<PagedTodoResponse>();
            Assert.NotNull(userBList);
            Assert.Empty(userBList.Items);

            // Act - User B attempts to access User A's Todo
            var getOtherResponse = await userBClient.GetAsync($"/api/todos/{createdTodo.Id}");
            var patchOtherResponse = await userBClient.PatchAsJsonAsync($"/api/todos/{createdTodo.Id}", new { title = "Hacked Title" });
            var deleteOtherResponse = await userBClient.DeleteAsync($"/api/todos/{createdTodo.Id}");

            // Assert Isolation (returns 404 to avoid leaking existence)
            Assert.Equal(HttpStatusCode.NotFound, getOtherResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, patchOtherResponse.StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, deleteOtherResponse.StatusCode);

            // Act - User A updates their Todo
            var updatePayload = new
            {
                title = "User A Updated Title",
                status = "inProgress",
                priority = "medium",
                dueDate = (DateTime?)null
            };

            var patchResponse = await userAClient.PatchAsJsonAsync($"/api/todos/{createdTodo.Id}", updatePayload);
            Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

            var updatedTodo = await patchResponse.Content.ReadFromJsonAsync<TodoResponse>();
            Assert.NotNull(updatedTodo);
            Assert.Equal("User A Updated Title", updatedTodo.Title);
            Assert.Equal(TodoStatus.InProgress, updatedTodo.Status);
            Assert.Equal(TodoPriority.Medium, updatedTodo.Priority);
            Assert.Null(updatedTodo.DueDate);
            Assert.True(updatedTodo.UpdatedAt >= createdTodo.UpdatedAt);

            // Act - User A deletes their Todo
            var deleteResponse = await userAClient.DeleteAsync($"/api/todos/{createdTodo.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // Act - User A tries to get the deleted Todo
            var getDeletedResponse = await userAClient.GetAsync($"/api/todos/{createdTodo.Id}");
            Assert.Equal(HttpStatusCode.NotFound, getDeletedResponse.StatusCode);
        }

        [Fact]
        public async Task Todo_Validation_Tests()
        {
            // Arrange - Login
            var client = _factory.CreateClient();
            var token = await RegisterAndGetTokenAsync(client, "validator@example.com", "Password123!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 1. Empty Title Validation
            var responseEmptyTitle = await client.PostAsJsonAsync("/api/todos", new CreateTodoRequest
            {
                Title = "",
                Priority = TodoPriority.Low
            }, _options);
            Assert.Equal(HttpStatusCode.BadRequest, responseEmptyTitle.StatusCode);

            // 2. Title Too Long Validation (>100 characters)
            var responseLongTitle = await client.PostAsJsonAsync("/api/todos", new CreateTodoRequest
            {
                Title = new string('A', 101),
                Priority = TodoPriority.Low
            }, _options);
            Assert.Equal(HttpStatusCode.BadRequest, responseLongTitle.StatusCode);

            // 3. Description Too Long Validation (>500 characters)
            var responseLongDesc = await client.PostAsJsonAsync("/api/todos", new CreateTodoRequest
            {
                Title = "Valid Title",
                Description = new string('B', 501),
                Priority = TodoPriority.Low
            }, _options);
            Assert.Equal(HttpStatusCode.BadRequest, responseLongDesc.StatusCode);

            // 4. Update Todo with empty patch payload (must fail validation)
            // Note: need a valid todo first to trigger patch body validation
            var createOk = await client.PostAsJsonAsync("/api/todos", new CreateTodoRequest { Title = "Valid", Priority = TodoPriority.Low }, _options);
            var todo = await createOk.Content.ReadFromJsonAsync<TodoResponse>();
            Assert.NotNull(todo);

            var responseEmptyPatch = await client.PatchAsJsonAsync($"/api/todos/{todo.Id}", new { });
            Assert.Equal(HttpStatusCode.BadRequest, responseEmptyPatch.StatusCode);
        }

        [Fact]
        public async Task Todo_List_Filtering_Sorting_Pagination_Tests()
        {
            // Arrange - Register & Login User
            var client = _factory.CreateClient();
            var token = await RegisterAndGetTokenAsync(client, "filteruser@example.com", "Password123!");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Create 5 different Todos
            var todo1 = await CreateTodoAsync(client, "Task Alpha", "Desc 1", TodoPriority.High, null);
            var todo2 = await CreateTodoAsync(client, "Task Beta", "Desc 2", TodoPriority.Medium, null);
            var todo3 = await CreateTodoAsync(client, "Task Gamma", "Desc 3", TodoPriority.Low, null);
            var todo4 = await CreateTodoAsync(client, "Task Delta", "Desc 4", TodoPriority.Low, null);
            var todo5 = await CreateTodoAsync(client, "Task Epsilon", "Desc 5", TodoPriority.High, null);

            // Set some InProgress/Completed states to test status filtering
            var p2 = await client.PatchAsJsonAsync($"/api/todos/{todo2.Id}", new { status = "inProgress" });
            var p3 = await client.PatchAsJsonAsync($"/api/todos/{todo3.Id}", new { status = "completed" });
            Assert.Equal(HttpStatusCode.OK, p2.StatusCode);
            Assert.Equal(HttpStatusCode.OK, p3.StatusCode);

            // 1. Status Filter: pending
            var resStatus = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?status=pending");
            Assert.NotNull(resStatus);
            Assert.Equal(3, resStatus.Items.Count); // Alpha, Delta, Epsilon should be pending
            Assert.Contains(resStatus.Items, t => t.Title == "Task Alpha");
            Assert.Contains(resStatus.Items, t => t.Title == "Task Delta");
            Assert.Contains(resStatus.Items, t => t.Title == "Task Epsilon");

            // 2. Priority Filter: high
            var resPriority = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?priority=high");
            Assert.NotNull(resPriority);
            Assert.Equal(2, resPriority.Items.Count); // Alpha, Epsilon
            Assert.Contains(resPriority.Items, t => t.Title == "Task Alpha");
            Assert.Contains(resPriority.Items, t => t.Title == "Task Epsilon");

            // 3. Keyword Search: "task" (all) vs "gamma" (1)
            var resKeyword = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?keyword=gamma");
            Assert.NotNull(resKeyword);
            Assert.Single(resKeyword.Items);
            Assert.Equal("Task Gamma", resKeyword.Items[0].Title);

            // 4. Pagination: pageSize=2
            var resPageSize = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?pageSize=2");
            Assert.NotNull(resPageSize);
            Assert.Equal(2, resPageSize.Items.Count);
            Assert.Equal(5, resPageSize.TotalCount);
            Assert.Equal(3, resPageSize.TotalPages);

            // 5. Sorting: sortBy=title&sortDirection=asc
            var resSortTitle = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?sortBy=title&sortDirection=asc&pageSize=10");
            Assert.NotNull(resSortTitle);
            Assert.Equal(5, resSortTitle.Items.Count);
            Assert.Equal("Task Alpha", resSortTitle.Items[0].Title);
            Assert.Equal("Task Beta", resSortTitle.Items[1].Title);
            Assert.Equal("Task Delta", resSortTitle.Items[2].Title);
            Assert.Equal("Task Epsilon", resSortTitle.Items[3].Title);
            Assert.Equal("Task Gamma", resSortTitle.Items[4].Title);

            // 6. Sorting by Priority: sortBy=priority&sortDirection=asc (Low -> Medium -> High)
            var resSortPriority = await client.GetFromJsonAsync<PagedTodoResponse>("/api/todos?sortBy=priority&sortDirection=asc&pageSize=10");
            Assert.NotNull(resSortPriority);
            Assert.Equal(5, resSortPriority.Items.Count);
            // Low items first
            Assert.Equal(TodoPriority.Low, resSortPriority.Items[0].Priority);
            Assert.Equal(TodoPriority.Low, resSortPriority.Items[1].Priority);
            // Medium item next
            Assert.Equal(TodoPriority.Medium, resSortPriority.Items[2].Priority);
            // High items last
            Assert.Equal(TodoPriority.High, resSortPriority.Items[3].Priority);
            Assert.Equal(TodoPriority.High, resSortPriority.Items[4].Priority);
        }

        private async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string password)
        {
            var registerRequest = new RegisterRequest
            {
                Email = email,
                Password = password,
                DisplayName = "User DisplayName"
            };
            var regRes = await client.PostAsJsonAsync("/api/auth/register", registerRequest, _options);
            Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);

            var loginRequest = new LoginRequest
            {
                Email = email,
                Password = password
            };
            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest, _options);
            Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
            var result = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
            return result!.AccessToken;
        }

        private async Task<TodoResponse> CreateTodoAsync(HttpClient client, string title, string desc, TodoPriority priority, DateTime? dueDate)
        {
            var request = new CreateTodoRequest
            {
                Title = title,
                Description = desc,
                Priority = priority,
                DueDate = dueDate
            };
            var response = await client.PostAsJsonAsync("/api/todos", request, _options);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<TodoResponse>())!;
        }
    }
}
