# -----------------------------
# Build Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj .
RUN dotnet restore HealthCardApi.csproj

# Copy the rest of the source code
COPY . .

# Publish the application
RUN dotnet publish HealthCardApi.csproj -c Release -o /app/publish

# -----------------------------
# Runtime Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install SkiaSharp dependencies for PDF processing
RUN apt-get update && \
    apt-get install -y libfontconfig1 libfreetype6 libharfbuzz0b && \
    rm -rf /var/lib/apt/lists/*

# Copy published files from build stage
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENTRYPOINT ["dotnet", "HealthCardApi.dll"]