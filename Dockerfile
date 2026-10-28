# Build from the repository root: docker build -f Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/LMSFree.Domain/LMSFree.Domain.csproj src/LMSFree.Domain/
COPY src/LMSFree.Infrastructure/LMSFree.Infrastructure.csproj src/LMSFree.Infrastructure/
COPY src/LMSFree.Api/LMSFree.Api.csproj src/LMSFree.Api/
RUN dotnet restore src/LMSFree.Api/LMSFree.Api.csproj
COPY src src
RUN dotnet publish src/LMSFree.Api/LMSFree.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    Storage__Path=/data/uploads
COPY --from=build /app/publish .
RUN mkdir -p /data/uploads && chown -R $APP_UID /data
USER $APP_UID
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "LMSFree.Api.dll"]
