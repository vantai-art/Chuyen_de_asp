FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["ngovantai_2123110272.csproj", "./"]
RUN dotnet restore "ngovantai_2123110272.csproj"

COPY . .
WORKDIR "/src"
RUN dotnet build "ngovantai_2123110272.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ngovantai_2123110272.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ngovantai_2123110272.dll"]