FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /

COPY src/Shared/StockFlow.Shared/StockFlow.Shared.csproj src/Shared/StockFlow.Shared/
COPY src/Shared/StockFlow.Contracts/StockFlow.Contracts.csproj src/Shared/StockFlow.Contracts/
COPY src/Services/NotificationService/NotificationService.csproj src/Services/NotificationService/

RUN dotnet restore src/Services/NotificationService/NotificationService.csproj

COPY src/Shared/StockFlow.Shared/ src/Shared/StockFlow.Shared/
COPY src/Shared/StockFlow.Contracts/ src/Shared/StockFlow.Contracts/
COPY src/Services/NotificationService/ src/Services/NotificationService/

WORKDIR /src/Services/NotificationService
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5003

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NotificationService.dll"]
