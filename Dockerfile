# Use official .NET SDK image for build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set working directory
WORKDIR /src

# Copy csproj and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy everything else
COPY . ./

# Publish the app
RUN dotnet publish -c Release -o /app/publish

# Use runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0

# Set working directory
WORKDIR /app

# Copy published files
COPY --from=build /app/publish .

# Expose port
EXPOSE 5048

# Start the app
ENTRYPOINT ["dotnet", "HealthCardApi.dll"]
