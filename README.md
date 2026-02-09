# Snipland Launcher

Легкий кроссплатформенный лаунчер для Minecraft с системой автоматического обновления сборок.

## Как собрать и запустить

### Требования
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Сборка и запуск в режиме разработки
```bash
dotnet run --project SniplandLauncher/SniplandLauncher.csproj
```

### Создание готовой сборки (Publish)

**Для Windows:**
```bash
dotnet publish SniplandLauncher/SniplandLauncher.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

**Для Linux:**
```bash
dotnet publish SniplandLauncher/SniplandLauncher.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true
```

**Для macOS:**
```bash
dotnet publish SniplandLauncher/SniplandLauncher.csproj -c Release -r osx-x64 --self-contained true /p:PublishSingleFile=true
```

Результат будет находиться в папке `SniplandLauncher/bin/Release/net8.0/[runtime]/publish/`.

## Особенности
- Автоматическая установка Forge и Fabric.
- Автоматическое управление версиями Java (JRE 8, 17, 21).
- Синхронизация файлов сборки с проверкой хэшей.
- Авторизация через Ely.by.