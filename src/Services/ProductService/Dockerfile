FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /

COPY src/Shared/StockFlow.Shared/StockFlow.Shared.csproj src/Shared/StockFlow.Shared/
COPY src/Shared/StockFlow.Contracts/StockFlow.Contracts.csproj src/Shared/StockFlow.Contracts/
COPY src/Services/ProductService/ProductService.csproj src/Services/ProductService/

RUN dotnet restore src/Services/ProductService/ProductService.csproj

COPY src/Shared/StockFlow.Shared/ src/Shared/StockFlow.Shared/
COPY src/Shared/StockFlow.Contracts/ src/Shared/StockFlow.Contracts/
COPY src/Services/ProductService/ src/Services/ProductService/

WORKDIR /src/Services/ProductService
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5001
EXPOSE 5011

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ProductService.dll"]
