# Builds any business API. Usage:
#   docker build -f deploy/docker/api.Dockerfile --build-arg SERVICE=Order.Api -t restaurant-ai/order-api .
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG SERVICE
WORKDIR /src
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/ src/
RUN test -n "$SERVICE" || (echo "SERVICE build arg is required (e.g. Order.Api)" && exit 1)
RUN dotnet publish "src/Api/${SERVICE}/${SERVICE}.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
ARG SERVICE
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    SERVICE_DLL=${SERVICE}.dll
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet \"$SERVICE_DLL\""]
