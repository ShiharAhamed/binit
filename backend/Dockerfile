FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/SmartWaste.Domain/SmartWaste.Domain.csproj src/SmartWaste.Domain/
COPY src/SmartWaste.Application/SmartWaste.Application.csproj src/SmartWaste.Application/
COPY src/SmartWaste.Infrastructure/SmartWaste.Infrastructure.csproj src/SmartWaste.Infrastructure/
COPY src/SmartWaste.Api/SmartWaste.Api.csproj src/SmartWaste.Api/
RUN dotnet restore src/SmartWaste.Api/SmartWaste.Api.csproj

COPY src/ src/
RUN dotnet publish src/SmartWaste.Api/SmartWaste.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
CMD ["sh", "-c", "exec dotnet SmartWaste.Api.dll --urls \"http://0.0.0.0:${PORT:-10000}\""]
