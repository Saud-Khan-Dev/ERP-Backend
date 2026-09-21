# Build context must be the `src` folder (the service depends on BuildingBlocks):
#   docker build -f Services/AssetsManagement/AssetsManagement.Api/Dockerfile -t erp-assetsmanagement .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first so `dotnet restore` is cached until a csproj changes
COPY ["BuildingBlocks/BuildingBlock/BuildingBlock.csproj", "BuildingBlocks/BuildingBlock/"]
COPY ["Services/AssetsManagement/AssetsManagement.Domain/AssetsManagement.Domain.csproj", "Services/AssetsManagement/AssetsManagement.Domain/"]
COPY ["Services/AssetsManagement/AssetsManagement.Application/AssetsManagement.Application.csproj", "Services/AssetsManagement/AssetsManagement.Application/"]
COPY ["Services/AssetsManagement/AssetsManagement.Infrastructure/AssetsManagement.Infrastructure.csproj", "Services/AssetsManagement/AssetsManagement.Infrastructure/"]
COPY ["Services/AssetsManagement/AssetsManagement.Api/AssetsManagement.Api.csproj", "Services/AssetsManagement/AssetsManagement.Api/"]
RUN dotnet restore "Services/AssetsManagement/AssetsManagement.Api/AssetsManagement.Api.csproj"

COPY BuildingBlocks/BuildingBlock/ BuildingBlocks/BuildingBlock/
COPY Services/AssetsManagement/ Services/AssetsManagement/
WORKDIR /src/Services/AssetsManagement/AssetsManagement.Api
RUN dotnet build "AssetsManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AssetsManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AssetsManagement.Api.dll"]
