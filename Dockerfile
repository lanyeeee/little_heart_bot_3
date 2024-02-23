FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["little_heart_bot_3/little_heart_bot_3.csproj", "little_heart_bot_3/"]
RUN dotnet restore "little_heart_bot_3/little_heart_bot_3.csproj"
COPY [".", "."]
WORKDIR "/src/little_heart_bot_3"
RUN dotnet build "little_heart_bot_3.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "little_heart_bot_3.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=true 

FROM base AS final
WORKDIR /app
COPY --chmod=777 --from=publish /app/publish .
ENTRYPOINT ["./little_heart_bot_3"]
