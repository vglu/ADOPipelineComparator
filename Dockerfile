FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore ADOPipelineComparator.sln
RUN dotnet publish src/ADOPipelineComparator.Web/ADOPipelineComparator.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DB_PATH=/app/data/data.db

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ADOPipelineComparator.Web.dll"]
