# Multi-stage build for SQL Server Reviewer MCP
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY SqlServerMcp/SqlServerMcp.csproj SqlServerMcp/
RUN dotnet restore SqlServerMcp/SqlServerMcp.csproj

COPY SqlServerMcp/ SqlServerMcp/
RUN dotnet publish SqlServerMcp/SqlServerMcp.csproj -c Release -o /app

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# MCP servers communicate via stdio, so no EXPOSE needed
ENTRYPOINT ["dotnet", "SqlServerMcp.dll"]
