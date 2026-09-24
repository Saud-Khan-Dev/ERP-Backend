# Build context must be the `src` folder (the service depends on BuildingBlocks):
#   docker build -f Services/Identity/Identity.Api/Dockerfile -t erp-identity .

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
COPY ["Services/Identity/Identity.Domain/Identity.Domain.csproj", "Services/Identity/Identity.Domain/"]
COPY ["Services/Identity/Identity.Application/Identity.Application.csproj", "Services/Identity/Identity.Application/"]
COPY ["Services/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj", "Services/Identity/Identity.Infrastructure/"]
COPY ["Services/Identity/Identity.Api/Identity.Api.csproj", "Services/Identity/Identity.Api/"]
RUN dotnet restore "Services/Identity/Identity.Api/Identity.Api.csproj"

COPY BuildingBlocks/ BuildingBlocks/
COPY Services/Identity/ Services/Identity/
WORKDIR /src/Services/Identity/Identity.Api
RUN dotnet build "Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Identity.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Identity.Api.dll"]
