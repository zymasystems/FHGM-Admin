FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Frontend/FHGM.Admin.csproj Frontend/
RUN dotnet restore Frontend/FHGM.Admin.csproj
COPY Frontend/ Frontend/
RUN dotnet publish Frontend/FHGM.Admin.csproj -c Release -o /app --no-restore /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "FHGM.Admin.dll"]
