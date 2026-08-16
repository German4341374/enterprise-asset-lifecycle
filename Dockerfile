# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS restore
WORKDIR /source
COPY Directory.Build.props global.json EnterpriseAssetLifecycle.slnx ./
COPY src/EnterpriseAssetLifecycle.Web/EnterpriseAssetLifecycle.Web.csproj src/EnterpriseAssetLifecycle.Web/
COPY src/EnterpriseAssetLifecycle.Web/packages.lock.json src/EnterpriseAssetLifecycle.Web/
RUN dotnet restore src/EnterpriseAssetLifecycle.Web/EnterpriseAssetLifecycle.Web.csproj --locked-mode

FROM restore AS build
COPY src/EnterpriseAssetLifecycle.Web/ src/EnterpriseAssetLifecycle.Web/
RUN dotnet publish src/EnterpriseAssetLifecycle.Web/EnterpriseAssetLifecycle.Web.csproj \
    --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.11 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl --fail --silent http://127.0.0.1:8080/health/ready || exit 1
ENTRYPOINT ["dotnet", "EnterpriseAssetLifecycle.Web.dll"]
