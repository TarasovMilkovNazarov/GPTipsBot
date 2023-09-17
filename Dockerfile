#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/runtime:7.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["GPTipsBot/GPTipsBot.csproj", "GPTipsBot/"]
RUN dotnet restore "GPTipsBot/GPTipsBot.csproj"
COPY . .
WORKDIR "/src/GPTipsBot"
RUN dotnet build "GPTipsBot.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GPTipsBot.csproj" -c Release -o /app/publish /p:UseAppHost=false

# use last postgres image
FROM postgres:latest
WORKDIR /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GPTipsBot.dll"]