FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 7005

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["Services/Files/Files.API/Files.API.csproj", "Services/Files/Files.API/"]
RUN dotnet restore "Services/Files/Files.API/Files.API.csproj"
COPY . .
WORKDIR "/src/Services/Files/Files.API"
RUN dotnet build "Files.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Files.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Files.API.dll"]
