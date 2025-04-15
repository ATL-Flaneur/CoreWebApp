# Use the official .NET 9 SDK image to build the app.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies.
COPY ["Server.csproj", "./"]
RUN dotnet restore "Server.csproj"

# Copy the rest of the application code.
COPY . .

# Build the application.
RUN dotnet build "Server.csproj" -c Release -o /app/build

# Publish the application.
RUN dotnet publish "Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Use the official .NET 9 runtime image for the final stage.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Server.dll"]
