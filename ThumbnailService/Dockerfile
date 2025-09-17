FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

# Cloud Run expects services to listen on PORT (default: 8080)
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG configuration=Release
WORKDIR /src

# Copy csproj and restore as distinct layers
COPY ["ThumbnailService.csproj", ""]
RUN dotnet restore "ThumbnailService.csproj"

# Copy everything else
COPY . .
RUN dotnet build "ThumbnailService.csproj" -c $configuration -o /app/build

FROM build AS publish
ARG configuration=Release
RUN dotnet publish "ThumbnailService.csproj" -c $configuration -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ThumbnailService.dll"]