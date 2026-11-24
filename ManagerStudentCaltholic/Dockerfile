# ==============================================================================
# ManagerStudentCaltholic - Production Multi-Stage Dockerfile
# Build Context : Repository Root (.)
# Build Command : docker build --platform linux/amd64 -f ManagerStudentCaltholic/Dockerfile -t <image_ref> .
# ==============================================================================

# ------------------------------------------------------------------------------
# Stage 1: Build & Publish (.NET 8 SDK)
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1. Copy .csproj first so NuGet restore layer is cached across source-only edits
COPY ["ManagerStudentCaltholic/ManagerStudentCaltholic.csproj", "ManagerStudentCaltholic/"]
RUN dotnet restore "ManagerStudentCaltholic/ManagerStudentCaltholic.csproj"

# 2. Copy application source and publish Release without AppHost or debug symbols
COPY ["ManagerStudentCaltholic/", "ManagerStudentCaltholic/"]
WORKDIR /src/ManagerStudentCaltholic
RUN dotnet publish "ManagerStudentCaltholic.csproj" \
        -c Release \
        -o /app/publish \
        --no-restore \
        /p:UseAppHost=false \
        /p:DebugType=None \
        /p:DebugSymbols=false \
    # Defense-in-depth: ensure local/dev secret config files can never enter the image
    && rm -f /app/publish/appsettings.Development.json \
             /app/publish/appsettings.Production.json \
             /app/publish/appsettings.*.local.json \
             /app/publish/appsettings-copy.json

# ------------------------------------------------------------------------------
# Stage 2: Slim Production Runtime (< 250 MB target)
#
# Base image justification:
# - Chosen   : mcr.microsoft.com/dotnet/aspnet:8.0-alpine (~108 MB base + ~41 MB
#              system packages + ~40 MB published IL = ~189 MB total, safely < 250 MB).
# - Rejected : mcr.microsoft.com/dotnet/aspnet:8.0 (Debian bookworm-slim is ~216 MB
#              base alone, reaching ~265+ MB with fonts + app, exceeding 250 MB).
# - Verified NuGet compatibility on Alpine (musl libc, zero native runtimes/ assets):
#   * ClosedXML 0.102.3 : pure managed + SixLabors.Fonts (embeds CarlitoBare; we also
#     install fontconfig + ttf-dejavu for full Vietnamese glyph coverage in AdjustToContents()).
#   * QRCoder 1.6.0     : uses PngByteQRCode only (pure managed PNG, no libgdiplus).
#   * PdfPig 0.1.9, NCalc 7.0.0, Npgsql 8.0.11, Hangfire 1.8.14,
#     StackExchange.Redis 2.8.16, Serilog: 100% pure managed .NET assemblies.
# - Timezone & Vietnamese globalization:
#   * tzdata provides Asia/Ho_Chi_Minh for Hangfire recurring jobs.
#   * icu-libs + icu-data-full with DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
#     enables full vi-VN culture/collation support on Alpine.
# ------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

ENV TZ=Asia/Ho_Chi_Minh \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    ASPNETCORE_HTTP_PORTS=8080

# Install timezone data, full ICU (vi-VN), and TrueType fonts for ClosedXML AdjustToContents()
RUN apk add --no-cache \
        tzdata \
        icu-libs \
        icu-data-full \
        fontconfig \
        ttf-dejavu \
    && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime \
    && echo "$TZ" > /etc/timezone \
    && fc-cache -f

# Pre-create runtime writable & host-mounted directories owned by built-in non-root user ($APP_UID)
RUN mkdir -p \
        /app/logs \
        /app/dataprotection-keys \
        /app/wwwroot/uploads/lesson_plans \
        /app/secrets \
        /app/PrivateUploads/exports \
        /app/PrivateUploads/lesson_plans \
        /app/PrivateUploads/baptism_certs \
        /app/PrivateUploads/curriculums \
        /app/PrivateUploads/cloud_fallback \
        /app/PrivateUploads/cloud_tmp \
        /app/PrivateUploads/tmp_logs \
    && chown -R $APP_UID:$APP_UID /app

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

# Run as built-in .NET 8 non-root user (UID 1654)
USER $APP_UID

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 CMD wget -qO- http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "ManagerStudentCaltholic.dll"]
