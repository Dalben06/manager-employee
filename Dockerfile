FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore only the API's project graph so this layer is cached until a project file changes.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj Source/ManagerEmployee.Api/
COPY Source/ManagerEmployee.Application/ManagerEmployee.Application.csproj Source/ManagerEmployee.Application/
COPY Source/ManagerEmployee.Domain/ManagerEmployee.Domain.csproj Source/ManagerEmployee.Domain/
COPY Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj Source/ManagerEmployee.Infrastructure/
RUN dotnet restore Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj

COPY Source/ Source/
RUN dotnet publish Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ManagerEmployee.Api.dll"]
