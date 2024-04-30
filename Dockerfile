FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

COPY *.sln .
COPY GPTipsBot/GPTipsBot.csproj ./GPTipsBot/
RUN dotnet restore GPTipsBot/GPTipsBot.csproj

COPY GPTipsBot/. ./GPTipsBot/
WORKDIR /src/GPTipsBot
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/publish ./
COPY scripts/*.conf ./fluentbit-conf/

ARG VERSION
ARG COMMITHASH
ENV GPTIPSBOT_VERSION=$VERSION
ENV GPTIPSBOT_COMMITHASH=$COMMITHASH

ENTRYPOINT ["dotnet", "GPTipsBot.dll"]