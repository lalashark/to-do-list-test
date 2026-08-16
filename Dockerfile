# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source

# Copy csproj and restore dependencies
COPY TodoList.Api/TodoList.Api.csproj TodoList.Api/
RUN dotnet restore TodoList.Api/TodoList.Api.csproj

# Copy all source files and publish the API project
COPY TodoList.Api/ TodoList.Api/
WORKDIR /source/TodoList.Api
RUN dotnet publish -c Release -o /app

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Explicitly configure ASP.NET Core to listen on port 8080
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TodoList.Api.dll"]
