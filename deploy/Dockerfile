FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/Mems.Web/Mems.Web.csproj src/Mems.Web/
RUN dotnet restore src/Mems.Web/Mems.Web.csproj
COPY src/Mems.Web/ src/Mems.Web/
RUN dotnet publish src/Mems.Web/Mems.Web.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out .
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    Storage__Root=/home/mems
EXPOSE 8080
ENTRYPOINT ["dotnet", "Mems.Web.dll"]
