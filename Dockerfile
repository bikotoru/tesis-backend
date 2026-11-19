FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilar
WORKDIR /api
COPY api/Backend.csproj ./
RUN dotnet restore
COPY api/ ./
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=compilar /app ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Backend.dll"]
