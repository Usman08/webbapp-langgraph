FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/SalesInvoice.Api/SalesInvoice.Api.csproj", "src/SalesInvoice.Api/"]
COPY ["src/SalesInvoice.Application/SalesInvoice.Application.csproj", "src/SalesInvoice.Application/"]
COPY ["src/SalesInvoice.Domain/SalesInvoice.Domain.csproj", "src/SalesInvoice.Domain/"]
COPY ["src/SalesInvoice.Infrastructure/SalesInvoice.Infrastructure.csproj", "src/SalesInvoice.Infrastructure/"]
RUN dotnet restore "src/SalesInvoice.Api/SalesInvoice.Api.csproj"

COPY . .
RUN dotnet publish "src/SalesInvoice.Api/SalesInvoice.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SalesInvoice.Api.dll"]
