# Pathdle Generator — Cloud Run Job image. Build from monorepo root:
#   docker build -f apps/generator/Dockerfile -t pathdle-generator .
# Override command/args on the Job, e.g. generate-daily --today

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/api/Pathdle.Application/Pathdle.Application.csproj apps/api/Pathdle.Application/
COPY apps/api/Pathdle.Infrastructure/Pathdle.Infrastructure.csproj apps/api/Pathdle.Infrastructure/
COPY apps/generator/Pathdle.Generator/Pathdle.Generator.csproj apps/generator/Pathdle.Generator/
RUN dotnet restore apps/generator/Pathdle.Generator/Pathdle.Generator.csproj

COPY apps/api/Pathdle.Application/ apps/api/Pathdle.Application/
COPY apps/api/Pathdle.Infrastructure/ apps/api/Pathdle.Infrastructure/
COPY apps/generator/Pathdle.Generator/ apps/generator/Pathdle.Generator/

RUN dotnet publish apps/generator/Pathdle.Generator/Pathdle.Generator.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production
USER $APP_UID
ENTRYPOINT ["dotnet", "Pathdle.Generator.dll"]
