# Build context is expected to be `./backend` (see docker-compose.yml at repo root).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY FleetDelivery.sln ./
COPY src/FleetDelivery.BuildingBlocks/FleetDelivery.BuildingBlocks.csproj src/FleetDelivery.BuildingBlocks/
COPY src/FleetDelivery.Api/FleetDelivery.Api.csproj src/FleetDelivery.Api/
RUN dotnet restore src/FleetDelivery.Api/FleetDelivery.Api.csproj

COPY src/FleetDelivery.BuildingBlocks/ src/FleetDelivery.BuildingBlocks/
COPY src/FleetDelivery.Api/ src/FleetDelivery.Api/
RUN dotnet publish src/FleetDelivery.Api/FleetDelivery.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "FleetDelivery.Api.dll"]
