# Build context must be the `src` folder (the service depends on BuildingBlocks):
#   docker build -f Services/HRM/HRM.Api/Dockerfile -t erp-hrm .

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
COPY ["Services/HRM/HRM.Domain/HRM.Domain.csproj", "Services/HRM/HRM.Domain/"]
COPY ["Services/HRM/HRM.Application/HRM.Application.csproj", "Services/HRM/HRM.Application/"]
COPY ["Services/HRM/HRM.Infrastructure/HRM.Infrastructure.csproj", "Services/HRM/HRM.Infrastructure/"]
COPY ["Services/HRM/HRM.Api/HRM.Api.csproj", "Services/HRM/HRM.Api/"]
RUN dotnet restore "Services/HRM/HRM.Api/HRM.Api.csproj"

COPY BuildingBlocks/ BuildingBlocks/
COPY Services/HRM/ Services/HRM/
WORKDIR /src/Services/HRM/HRM.Api
RUN dotnet build "HRM.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "HRM.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "HRM.Api.dll"]
