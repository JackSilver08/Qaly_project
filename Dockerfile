# ========================================
# Qaly Project - Dockerfile
# Multi-stage build for .NET 10
# ========================================

# ---- Stage 1: Base runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

# ---- Stage 2: Development (hot reload) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS development
WORKDIR /app
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
ENV ASPNETCORE_ENVIRONMENT=Development

# Chỉ copy csproj trước để cache NuGet restore
COPY src/Qaly.Domain/*.csproj src/Qaly.Domain/
COPY src/Qaly.Application/*.csproj src/Qaly.Application/
COPY src/Qaly.Infrastructure/*.csproj src/Qaly.Infrastructure/
COPY src/Qaly.Web/*.csproj src/Qaly.Web/
COPY Qaly_project.slnx .

RUN dotnet restore src/Qaly.Web/Qaly.Web.csproj

# Copy toàn bộ source
COPY . .

# Dev mode: use the container URL from ASPNETCORE_URLS. Launch profiles are
# workstation-only and may require a developer HTTPS certificate that does not
# exist inside the container.
ENTRYPOINT ["dotnet", "watch", "run", "--project", "src/Qaly.Web", "--no-launch-profile"]

# ---- Stage 3: Build for production ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj và restore (cached layer)
COPY src/Qaly.Domain/*.csproj src/Qaly.Domain/
COPY src/Qaly.Application/*.csproj src/Qaly.Application/
COPY src/Qaly.Infrastructure/*.csproj src/Qaly.Infrastructure/
COPY src/Qaly.Web/*.csproj src/Qaly.Web/
COPY Qaly_project.slnx .

RUN dotnet restore src/Qaly.Web/Qaly.Web.csproj

# Copy source và build
COPY . .
WORKDIR /src/src/Qaly.Web
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- Stage 4: Production runtime ----
FROM base AS production
WORKDIR /app

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
RUN mkdir -p /app/.keys /app/dp-keys /app/uploads \
    && chown -R $APP_UID:$APP_UID /app/.keys /app/dp-keys /app/uploads

# Official .NET runtime images expose a built-in non-root application UID.
USER $APP_UID
ENTRYPOINT ["dotnet", "Qaly.Web.dll"]
