# ── MediMind API Dockerfile ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy csproj files first for layer caching
COPY src/MediMind.Core/MediMind.Core.csproj src/MediMind.Core/
COPY src/MediMind.Infrastructure/MediMind.Infrastructure.csproj src/MediMind.Infrastructure/
COPY src/MediMind.API/MediMind.API.csproj src/MediMind.API/

RUN dotnet restore src/MediMind.API/MediMind.API.csproj

# Copy all source
COPY src/ src/

WORKDIR /src/src/MediMind.API
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediMind.API.dll"]
