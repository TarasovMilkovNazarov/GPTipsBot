FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY *.sln .
COPY GPTipsBot/GPTipsBot.csproj ./GPTipsBot/
COPY OpenAI.SDK/OpenAI.csproj ./OpenAI.SDK/
RUN dotnet restore GPTipsBot/GPTipsBot.csproj
RUN dotnet restore OpenAI.SDK/OpenAI.csproj

COPY GPTipsBot/. ./GPTipsBot/
COPY OpenAI.SDK/. ./OpenAI.SDK/
WORKDIR /src/GPTipsBot
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish ./
COPY scripts/*.conf ./fluentbit-conf/

ARG VERSION
ARG COMMITHASH
ENV GPTIPSBOT_VERSION=$VERSION
ENV GPTIPSBOT_COMMITHASH=$COMMITHASH
ENV ASPNETCORE_URLS=http://+:80

ENTRYPOINT ["dotnet", "GPTipsBot.dll"]