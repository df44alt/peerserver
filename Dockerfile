# 1 - База для сборки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src
# Копируем все файлы репозитория
COPY . .
# Собираем приложение в каталог /app
RUN dotnet publish -c Release -o /app

# 2 - База для запуска
FROM mcr.microsoft.com/dotnet/runtime:8.0

WORKDIR /app
# Копируем готовый исполняемый пакет из статей build‑контейнера
COPY --from=build /app .
# Запускаем .dll
ENTRYPOINT ["dotnet", "PeerRegister.dll"]