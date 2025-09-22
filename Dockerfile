# Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Arrume.Web/Arrume.Web.csproj Arrume.Web/
RUN dotnet restore Arrume.Web/Arrume.Web.csproj
COPY . .
RUN dotnet publish Arrume.Web/Arrume.Web.csproj -c Release -o /app/out

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/out .
# Não fixa porta aqui; o Render define $PORT. Você passa ASPNETCORE_URLS no render.yaml.
ENTRYPOINT ["dotnet", "Arrume.Web.dll"]
