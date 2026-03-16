# ===============================
# BUILD STAGE
# ===============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /app

# Copy ONLY the API project file (root-level)
COPY pqy-server.csproj ./

# Restore dependencies for API project only
RUN dotnet restore pqy-server.csproj

# Copy the rest of the source code
COPY . .

# Publish the API project
RUN dotnet publish pqy-server.csproj -c Release -o /app/out


# ===============================
# RUNTIME STAGE
# ===============================
FROM mcr.microsoft.com/dotnet/aspnet:9.0

WORKDIR /app

COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "pqy-server.dll"]
