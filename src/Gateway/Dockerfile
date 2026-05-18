FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /

COPY src/Gateway/Gateway.csproj src/Gateway/
COPY src/Shared/StockFlow.Shared/StockFlow.Shared.csproj src/Shared/StockFlow.Shared/
RUN dotnet restore src/Gateway/Gateway.csproj

COPY src/Gateway/ src/Gateway/
COPY src/Shared/StockFlow.Shared/ src/Shared/StockFlow.Shared/

WORKDIR /src/Gateway
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.dll"]
