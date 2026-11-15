FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
ARG PROJECT
COPY src/ src/
RUN dotnet publish "$PROJECT" --configuration Release --output /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN install -d -o app -g app /app/data /app/uploads
COPY --from=build /publish/ ./
USER app
ENTRYPOINT ["dotnet"]
