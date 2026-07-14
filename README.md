# AVLN ToolBox

Windows WPF-приложение для установки и обновления единого пакета плагинов Avalon по версиям Autodesk Revit 2021–2025.

## Сборка

Требования:

- Windows 10/11 x64;
- .NET 8 SDK;
- Visual Studio 2022 с workload **.NET desktop development** либо `dotnet` CLI.

```powershell
dotnet restore .\AVLN_ToolBox.sln
dotnet build .\AVLN_ToolBox.sln -c Release -p:Platform=x64
```

Публикация self-contained:

```powershell
dotnet publish .\src\Avln.ToolBox\Avln.ToolBox.csproj -c Release -r win-x64 --self-contained true -o .\publish
```

## Локальные данные

```text
%LocalAppData%\AVLN\ToolBox\settings.json
%LocalAppData%\AVLN\ToolBox\installed.json
%LocalAppData%\AVLN\ToolBox\Logs\
%LocalAppData%\AVLN\ToolBox\Temp\
```

Путь установки по умолчанию:

```text
%AppData%\Autodesk\Revit\Addins\<год>
```

## GitHub Releases

Приложение читает публичные стабильные релизы репозитория `AVALON-IT-PLATFORM/AVLN_ToolBox` без токена.

Ожидаемые имена assets:

```text
Avalon.External.R21.zip
Avalon.External.R22.zip
Avalon.External.R23.zip
Avalon.External.R24.zip
Avalon.External.R25.zip
```

ZIP должен содержать `Avalon.External.addin`, `Avalon.External.dll` и зависимости сборки. При отсутствии релиза приложение показывает состояние `Версия пока недоступна`.

## Поведение установки

- Revit должен быть закрыт;
- пакет сначала распаковывается во временный staging-каталог;
- текущие файлы пакета резервируются;
- `installed.json` обновляется только после успешного копирования;
- при ошибке выполняется rollback;
- удаление затрагивает только файлы, записанные в локальном состоянии ToolBox.

Автообновление плагинов выполняется только при запуске ToolBox. Самообновление приложения в текущий MVP не входит.
