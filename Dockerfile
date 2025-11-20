# -----------------------------
# Build Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore dependencies
COPY HealthCardApi/*.csproj ./HealthCardApi/
RUN dotnet restore ./HealthCardApi/HealthCardApi.csproj

# Copy the rest of the source code
COPY HealthCardApi/. ./HealthCardApi/

# Publish the application
RUN dotnet publish ./HealthCardApi/HealthCardApi.csproj -c Release -o /app/publish

# -----------------------------
# Runtime Stage
# -----------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Expose port
EXPOSE 8080

# Environment variables for ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Start the application
ENTRYPOINT ["dotnet", "HealthCardApi.dll"]
