# ════════════════════════════════════════════════════════════════════════════
#  OmniX Platform — Dockerfile
#  ASP.NET Core 8 + Alpine + Non-root user + Dynamic PORT (Render-compatible)
# ════════════════════════════════════════════════════════════════════════════

# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Restore — cache layer (only re-runs when csproj changes)
COPY ["src/OmniX.API/OmniX.API.csproj",                         "src/OmniX.API/"]
COPY ["src/OmniX.Application/OmniX.Application.csproj",         "src/OmniX.Application/"]
COPY ["src/OmniX.Domain/OmniX.Domain.csproj",                   "src/OmniX.Domain/"]
COPY ["src/OmniX.Infrastructure/OmniX.Infrastructure.csproj",   "src/OmniX.Infrastructure/"]
RUN dotnet restore "src/OmniX.API/OmniX.API.csproj" --runtime linux-musl-x64

# Build
COPY . .
RUN dotnet publish "src/OmniX.API/OmniX.API.csproj" \
    --configuration Release \
    --runtime linux-musl-x64 \
    --self-contained false \
    --no-restore \
    --output /publish

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# ICU for Arabic locale support
RUN apk add --no-cache icu-libs wget

# Security: non-root user
RUN adduser --disabled-password --gecos "" omnix \
 && mkdir -p /app/logs \
 && chown -R omnix:omnix /app
USER omnix

COPY --from=build --chown=omnix:omnix /publish .

# Environment
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Render provides PORT=10000 as env variable — do NOT hardcode
EXPOSE 10000

# Health check using dynamic PORT
HEALTHCHECK --interval=30s --timeout=10s --start-period=90s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:${PORT:-10000}/health || exit 1

ENTRYPOINT ["dotnet", "OmniX.API.dll"]
