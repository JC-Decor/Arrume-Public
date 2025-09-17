# STAGE 1: build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# copia apenas o csproj para cachear restore
COPY Arrume.Web.csproj ./
RUN dotnet restore

# copia o resto e publica
COPY . ./
RUN dotnet publish -c Release -o /app/out

# STAGE 2: runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out ./

# Render setará $PORT; vamos expor algo "padrão" e o app bindará via ASPNETCORE_URLS
EXPOSE 8080

ENTRYPOINT ["dotnet", "Arrume.Web.dll"]
