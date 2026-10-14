# Build context must be the `src` folder (the service depends on BuildingBlocks):
#   docker build -f Services/Taxation/Taxation.Api/Dockerfile -t erp-taxation .

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files first so `dotnet restore` is cached until a csproj changes
COPY ["BuildingBlocks/BuildingBlock/BuildingBlock.csproj", "BuildingBlocks/BuildingBlock/"]
COPY ["BuildingBlocks/BuildingBlock.Authentication/BuildingBlock.Authentication.csproj", "BuildingBlocks/BuildingBlock.Authentication/"]
COPY ["Services/Taxation/Taxation.Domain/Taxation.Domain.csproj", "Services/Taxation/Taxation.Domain/"]
COPY ["Services/Taxation/Taxation.Application/Taxation.Application.csproj", "Services/Taxation/Taxation.Application/"]
COPY ["Services/Taxation/Taxation.Infrastructure/Taxation.Infrastructure.csproj", "Services/Taxation/Taxation.Infrastructure/"]
COPY ["Services/Taxation/Taxation.Api/Taxation.Api.csproj", "Services/Taxation/Taxation.Api/"]
RUN dotnet restore "Services/Taxation/Taxation.Api/Taxation.Api.csproj"

COPY BuildingBlocks/ BuildingBlocks/
COPY Services/Taxation/ Services/Taxation/
WORKDIR /src/Services/Taxation/Taxation.Api
RUN dotnet build "Taxation.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Taxation.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Taxation.Api.dll"]
