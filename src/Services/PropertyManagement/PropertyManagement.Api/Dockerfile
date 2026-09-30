# Build context must be the `src` folder (the service depends on BuildingBlocks):
#   docker build -f Services/PropertyManagement/PropertyManagement.Api/Dockerfile -t erp-propertymanagement .

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
COPY ["Services/PropertyManagement/PropertyManagement.Domain/PropertyManagement.Domain.csproj", "Services/PropertyManagement/PropertyManagement.Domain/"]
COPY ["Services/PropertyManagement/PropertyManagement.Application/PropertyManagement.Application.csproj", "Services/PropertyManagement/PropertyManagement.Application/"]
COPY ["Services/PropertyManagement/PropertyManagement.Infrastructure/PropertyManagement.Infrastructure.csproj", "Services/PropertyManagement/PropertyManagement.Infrastructure/"]
COPY ["Services/PropertyManagement/PropertyManagement.Api/PropertyManagement.Api.csproj", "Services/PropertyManagement/PropertyManagement.Api/"]
RUN dotnet restore "Services/PropertyManagement/PropertyManagement.Api/PropertyManagement.Api.csproj"

COPY BuildingBlocks/ BuildingBlocks/
COPY Services/PropertyManagement/ Services/PropertyManagement/
WORKDIR /src/Services/PropertyManagement/PropertyManagement.Api
RUN dotnet build "PropertyManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build --no-restore

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "PropertyManagement.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PropertyManagement.Api.dll"]
