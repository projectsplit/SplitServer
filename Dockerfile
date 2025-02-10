# Use the official .NET 8 SDK image as the base image for building the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the entire source code into the container
COPY . ./

# Restore dependencies
RUN dotnet restore

# Build and publish the app
RUN dotnet publish -c Release -o /out

# Use the official .NET 8 runtime image for the final image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

# Set the working directory
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /out .

# Expose the port your app is running on (usually 80 for APIs)
EXPOSE 5097

# Set the entry point for the container
ENTRYPOINT ["dotnet", "SplitServer.dll"]
