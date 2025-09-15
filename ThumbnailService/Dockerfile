# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . ./
RUN dotnet restore "ThumbnailService.csproj"
RUN dotnet publish "ThumbnailService.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV PORT=8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ThumbnailService.dll"]


