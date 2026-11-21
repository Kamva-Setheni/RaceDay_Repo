# ---------- Stage 1: build and publish ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first so the restore layer is cached until dependencies change
COPY RaceDay.Web.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- Stage 2: small runtime image ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    ApiBaseUrl=http://host.docker.internal:5003

COPY --from=build /app/publish .

# Run as the non-root "app" user that ships with the ASP.NET image
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "RaceDay.Web.dll"]
