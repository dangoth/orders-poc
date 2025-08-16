FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RestockingService/RestockingService.csproj", "RestockingService/"]
COPY ["Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "RestockingService/RestockingService.csproj"
COPY . .
WORKDIR "/src/RestockingService"
RUN dotnet build "RestockingService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "RestockingService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RestockingService.dll"]