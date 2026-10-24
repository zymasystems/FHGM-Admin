FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/FHGM.Admin/FHGM.Admin.csproj backend/FHGM.Admin/
RUN dotnet restore backend/FHGM.Admin/FHGM.Admin.csproj
COPY backend/ backend/
COPY frontend/ frontend/
RUN dotnet publish backend/FHGM.Admin/FHGM.Admin.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FHGM.Admin.dll"]
