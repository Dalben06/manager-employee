FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ManagerEmployee.slnx ./
COPY Source/ManagerEmployee.Api/ManagerEmployee.Api.csproj Source/ManagerEmployee.Api/
COPY Source/ManagerEmployee.Application/ManagerEmployee.Application.csproj Source/ManagerEmployee.Application/
COPY Source/ManagerEmployee.Domain/ManagerEmployee.Domain.csproj Source/ManagerEmployee.Domain/
COPY Source/ManagerEmployee.Infrastructure/ManagerEmployee.Infrastructure.csproj Source/ManagerEmployee.Infrastructure/
COPY Tests/ManagerEmployee.Api.IntegrationTests/ManagerEmployee.Api.IntegrationTests.csproj Tests/ManagerEmployee.Api.IntegrationTests/
COPY Tests/ManagerEmployee.Application.Tests/ManagerEmployee.Application.Tests.csproj Tests/ManagerEmployee.Application.Tests/
COPY Tests/ManagerEmployee.Infrastructure.Tests/ManagerEmployee.Infrastructure.Tests.csproj Tests/ManagerEmployee.Infrastructure.Tests/
RUN dotnet restore ManagerEmployee.slnx

COPY . .
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
