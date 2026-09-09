# Build context is expected to be `./backend` (see docker-compose.yml at repo root).
# Lives at the build-context root (rather than nested under src/FleetDelivery.Api)
# so PaaS providers that default to "<root>/Dockerfile" (e.g. Render) find it
# without needing a separate dockerfile-path setting.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

## Module projects are added over time (one per milestone), so this copies
## the whole `src/` tree rather than listing every .csproj individually —
## less Docker layer-cache granularity, but nothing to remember to update
## here each time a new module lands.
COPY Directory.Build.props ./
COPY FleetDelivery.sln ./
COPY src/ src/
RUN dotnet restore src/FleetDelivery.Api/FleetDelivery.Api.csproj
RUN dotnet publish src/FleetDelivery.Api/FleetDelivery.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as the base image's non-root app user rather than root — the app
# never writes into /app at runtime, so no ownership change is needed.
USER $APP_UID

ENTRYPOINT ["dotnet", "FleetDelivery.Api.dll"]
