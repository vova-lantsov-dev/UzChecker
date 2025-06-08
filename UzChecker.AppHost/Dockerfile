FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine3.21 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.21 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["UzChecker.AppHost/UzChecker.AppHost.csproj", "UzChecker.AppHost/"]
RUN dotnet restore "UzChecker.AppHost/UzChecker.AppHost.csproj"
COPY . .
WORKDIR "/src/UzChecker.AppHost"
RUN dotnet build "./UzChecker.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./UzChecker.AppHost.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "UzChecker.AppHost.dll"]
