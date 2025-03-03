FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:3fcf6f1e809c0553f9feb222369f58749af314af6f063f389cbd2f913b4ad556 AS build
WORKDIR /app

# Copy everything
# COPY . ./
COPY ./Properties ./
COPY ./Services ./
COPY ./appsettings.Production.json ./
COPY ./AquaAlertApi.csproj ./
COPY ./AquaAlertApi.http ./
COPY ./Program.cs ./

# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:b4bea3a52a0a77317fa93c5bbdb076623f81e3e2f201078d89914da71318b5d8
WORKDIR /app
RUN groupadd --gid 1234 emmanuel && useradd --uid 1234 --gid 1234 -m emmanuel
COPY --from=build /app/out .
USER emmanuel
ENTRYPOINT ["dotnet", "AquaAlertApi.dll"]