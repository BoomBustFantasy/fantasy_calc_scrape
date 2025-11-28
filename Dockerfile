# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy NuGet configuration
COPY ["nuget.config", "."]

# Copy project file and restore dependencies
COPY ["FantasyCalcScrape.csproj", "."]
RUN --mount=type=secret,id=nuget_token \
    NUGET_TOKEN=$(cat /run/secrets/nuget_token) \
    dotnet restore "./FantasyCalcScrape.csproj"

# Copy all source files
COPY . .
WORKDIR "/src/."

# Build the application
RUN dotnet build "./FantasyCalcScrape.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FantasyCalcScrape.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Create logs directory
RUN mkdir -p /app/logs

# Copy published application
COPY --from=publish /app/publish .

# Set environment variable for ASP.NET Core
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Create a non-root user for security
RUN adduser --disabled-password --home /app --gecos '' appuser && chown -R appuser /app
USER appuser

ENTRYPOINT ["dotnet", "FantasyCalcScrape.dll"]