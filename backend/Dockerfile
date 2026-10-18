# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (layer-cached on project files only)
COPY Checkout.sln ./
COPY src/Checkout.Domain/Checkout.Domain.csproj src/Checkout.Domain/
COPY src/Checkout.Application/Checkout.Application.csproj src/Checkout.Application/
COPY src/Checkout.Infrastructure/Checkout.Infrastructure.csproj src/Checkout.Infrastructure/
COPY src/Checkout.Api/Checkout.Api.csproj src/Checkout.Api/
COPY tests/Checkout.UnitTests/Checkout.UnitTests.csproj tests/Checkout.UnitTests/
COPY tests/Checkout.IntegrationTests/Checkout.IntegrationTests.csproj tests/Checkout.IntegrationTests/
RUN dotnet restore Checkout.sln

COPY . .
RUN dotnet publish src/Checkout.Api/Checkout.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
COPY --from=build /app/publish .
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Checkout.Api.dll"]
