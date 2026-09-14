# Pathdle API — build from monorepo root:
#   docker build -f apps/api/Dockerfile -t pathdle-api .
# Cloud Run listens on $PORT (default 8080).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/api/Pathdle.Api/Pathdle.Api.csproj apps/api/Pathdle.Api/
COPY apps/api/Pathdle.Application/Pathdle.Application.csproj apps/api/Pathdle.Application/
COPY apps/api/Pathdle.Infrastructure/Pathdle.Infrastructure.csproj apps/api/Pathdle.Infrastructure/
RUN dotnet restore apps/api/Pathdle.Api/Pathdle.Api.csproj

COPY apps/api/Pathdle.Api/ apps/api/Pathdle.Api/
COPY apps/api/Pathdle.Application/ apps/api/Pathdle.Application/
COPY apps/api/Pathdle.Infrastructure/ apps/api/Pathdle.Infrastructure/

RUN dotnet publish apps/api/Pathdle.Api/Pathdle.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Pathdle.Api.dll"]
