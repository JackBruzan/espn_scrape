# Use the official .NET 8 runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["ESPNScrape.csproj", "."]
RUN dotnet restore "ESPNScrape.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "ESPNScrape.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ESPNScrape.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create directories for downloads and logs
RUN mkdir -p /app/downloads/nfl_players
RUN mkdir -p /app/logs

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "ESPNScrape.dll"]
