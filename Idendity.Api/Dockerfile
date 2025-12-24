# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Idendity.Api/Idendity.Api.csproj", "Idendity.Api/"]
COPY ["Idendity.Application/Idendity.Application.csproj", "Idendity.Application/"]
COPY ["Idendity.Domain/Idendity.Domain.csproj", "Idendity.Domain/"]
COPY ["Idendity.Infrastructure/Idendity.Infrastructure.csproj", "Idendity.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "./Idendity.Api/Idendity.Api.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/Idendity.Api"
RUN dotnet build "./Idendity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Idendity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

# Copy published files
COPY --from=publish /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Idendity.Api.dll"]
