FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Backend/FHGM.Backend.csproj Backend/
RUN dotnet restore Backend/FHGM.Backend.csproj
COPY Backend/ Backend/
RUN dotnet publish Backend/FHGM.Backend.csproj -c Release -o /app --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FHGM.Backend.dll"]
