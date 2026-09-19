FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
WORKDIR /source
COPY global.json Directory.Build.props ./
COPY src/AlertDoors/ src/AlertDoors/
RUN dotnet restore src/AlertDoors/AlertDoors.csproj --locked-mode
RUN dotnet publish src/AlertDoors/AlertDoors.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0@sha256:8a153b5889d796b6450295b383596b13308c24c230515f8a7770ce1b94e0c460
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "AlertDoors.dll"]
CMD ["run"]
