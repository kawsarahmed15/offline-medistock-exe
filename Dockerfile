# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy solution and project files first to cache restore
COPY Medistock.sln ./
COPY src/Core/Medistock.Domain/Medistock.Domain.csproj src/Core/Medistock.Domain/
COPY src/Core/Medistock.Application/Medistock.Application.csproj src/Core/Medistock.Application/
COPY src/Infrastructure/Medistock.Infrastructure.Data/Medistock.Infrastructure.Data.csproj src/Infrastructure/Medistock.Infrastructure.Data/
COPY src/Infrastructure/Medistock.Infrastructure.Hardware/Medistock.Infrastructure.Hardware.csproj src/Infrastructure/Medistock.Infrastructure.Hardware/
COPY src/Infrastructure/Medistock.Infrastructure.Identity/Medistock.Infrastructure.Identity.csproj src/Infrastructure/Medistock.Infrastructure.Identity/
COPY src/Infrastructure/Medistock.Infrastructure.Sync/Medistock.Infrastructure.Sync.csproj src/Infrastructure/Medistock.Infrastructure.Sync/
COPY src/Services/Medistock.CloudApi/Medistock.CloudApi.csproj src/Services/Medistock.CloudApi/
COPY src/Services/Medistock.LocalServer/Medistock.LocalServer.csproj src/Services/Medistock.LocalServer/
COPY src/Shared/Medistock.Contracts/Medistock.Contracts.csproj src/Shared/Medistock.Contracts/

RUN dotnet restore src/Services/Medistock.CloudApi/Medistock.CloudApi.csproj

# Copy the rest of the source code
COPY . .

# Build and publish CloudApi
RUN dotnet publish src/Services/Medistock.CloudApi/Medistock.CloudApi.csproj -c Release -o /out

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /out .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "Medistock.CloudApi.dll"]
