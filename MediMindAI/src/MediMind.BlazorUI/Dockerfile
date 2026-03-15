# ── MediMind Blazor UI Dockerfile ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY src/MediMind.Core/MediMind.Core.csproj src/MediMind.Core/
COPY src/MediMind.BlazorUI/MediMind.BlazorUI.csproj src/MediMind.BlazorUI/

RUN dotnet restore src/MediMind.BlazorUI/MediMind.BlazorUI.csproj

COPY src/ src/

WORKDIR /src/src/MediMind.BlazorUI
RUN dotnet build -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
RUN dotnet publish -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediMind.BlazorUI.dll"]
