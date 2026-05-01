# ── Build stage ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore as a separate layer so Docker cache reuses it when only source changes
COPY WaveProcessor/WaveProcessor.csproj WaveProcessor/
RUN dotnet restore WaveProcessor/WaveProcessor.csproj

COPY . .
RUN dotnet publish WaveProcessor/WaveProcessor.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Cloud Run injects PORT=8080; tell ASP.NET Core to listen on it
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Run as non-root for security
RUN groupadd --system app && useradd --system --gid app --no-create-home app
USER app

ENTRYPOINT ["dotnet", "WaveProcessor.dll"]
