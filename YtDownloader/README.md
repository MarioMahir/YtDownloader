# YtDownloader

Descargador de videos multi-plataforma con UI moderna, basado en **yt-dlp** y construido con **WPF + MVVM** en C# .NET 8.

---

## Características

- **Descarga de video** con selección de calidad (4K, 1080p, 720p, etc.)
- **Descarga de solo audio** en MP3, AAC, FLAC, Opus, WAV, M4A
- **Descarga de listas de reproducción completas**, activable con el toggle "Playlist completa"
- **Cola de descargas** con concurrencia configurable
- **Notificación de sistema** al completar una descarga (activable en Ajustes)
- **Recorte básico de video** (inicio/fin) vía FFmpeg antes de encolar una descarga
- **Arrastrar y soltar URLs** directamente sobre el campo de URL
- **Historial persistente** con búsqueda, apertura de archivo y carpeta, y **exportación a CSV**
- **Metadatos del video**: título, canal, plataforma, duración, vistas, fecha
- **Multi-plataforma**: YouTube, Vimeo, Twitter/X, Twitch, Facebook y más
- **yt-dlp auto-gestionado**: se descarga y actualiza automáticamente desde GitHub
- **Incrustación de miniatura y metadatos** en el archivo final
- **Subtítulos automáticos** opcionales con idioma configurable
- **Proxy** configurable
- **Tema oscuro o claro**, configurable en Ajustes (se aplica al reiniciar la app), con chrome personalizado sin bordes de Windows

---

## Requisitos

| Requisito | Versión mínima |
|-----------|---------------|
| Windows   | 10 x64 (1809+)|
| .NET SDK  | 8.0           |
| Visual Studio | 2022 (o Rider) |

> **FFmpeg** es descargado automáticamente por la app (no por yt-dlp) la primera vez que se necesita, para extracción de audio, mezcla de streams y embeber miniaturas/metadatos.

---

## Cómo abrir y ejecutar

```bash
# 1. Clona o descomprime el proyecto
cd YtDownloader

# 2. Restaura paquetes NuGet
dotnet restore

# 3. Ejecuta en modo Debug
dotnet run --framework net8.0-windows

# O desde Visual Studio:
# Abrir YtDownloader.sln → F5
```

La primera vez que inicies la app, descargará automáticamente `yt-dlp.exe` desde el repositorio oficial de GitHub.

---

## Estructura del proyecto

```
YtDownloader/
├── Models/
│   ├── VideoInfo.cs          # Metadatos del video
│   ├── VideoFormat.cs        # Formato/calidad disponible
│   ├── DownloadItem.cs       # Ítem en la cola (observable)
│   ├── HistoryEntry.cs       # Entrada del historial
│   └── AppSettings.cs        # Configuración de usuario
│
├── Services/
│   ├── YtDlpService.cs       # Gestión de yt-dlp + ejecución
│   ├── DownloadQueueService.cs # Cola concurrente de descargas
│   ├── HistoryService.cs     # Historial persistido en JSON
│   └── SettingsService.cs    # Configuración persistida en JSON
│
├── ViewModels/
│   ├── MainViewModel.cs      # Orquestación y navegación
│   ├── DownloadViewModel.cs  # Lógica de descarga
│   ├── HistoryViewModel.cs   # Historial + búsqueda
│   └── SettingsViewModel.cs  # Preferencias
│
├── Views/
│   ├── MainWindow.xaml       # Ventana principal (chrome custom)
│   ├── DownloadView.xaml     # Pantalla de descarga
│   ├── HistoryView.xaml      # Pantalla de historial
│   └── SettingsView.xaml     # Pantalla de configuración
│
├── Commands/
│   └── RelayCommand.cs       # ICommand + AsyncRelayCommand
│
├── Converters/
│   └── Converters.cs         # Value converters para WPF
│
└── Assets/
    ├── app.ico               # Ícono de la aplicación
    └── Themes.xaml           # Tema oscuro completo (colores, estilos, hint text en inputs)
```

---

## Datos persistidos

Todos los datos se guardan en:
```
%LocalAppData%\YtDownloader\
├── settings.json    # Configuración del usuario
├── history.json     # Historial de descargas
└── bin\
    ├── yt-dlp.exe   # Binario auto-gestionado (se descarga/actualiza desde GitHub)
    ├── ffmpeg.exe   # Auto-gestionado (necesario para extraer audio, mezclar streams y embeber metadatos/miniaturas)
    └── ffprobe.exe  # Auto-gestionado, acompaña a ffmpeg.exe
```

---

## Paleta de colores

| Token         | Hex       | Uso                     |
|---------------|-----------|-------------------------|
| `BgDeep`      | `#0D0F14` | Fondo principal         |
| `BgSurface`   | `#161921` | Sidebar, barras         |
| `BgCard`      | `#1E2130` | Tarjetas, inputs        |
| `Accent`      | `#FF3B5F` | Botón primario, foco    |
| `TextPrimary` | `#F0F2FF` | Texto principal         |
| `Success`     | `#00C78C` | Descarga completada     |
| `Error`       | `#FF4757` | Errores                 |

---

## Dependencias NuGet

| Paquete | Versión | Uso |
|---------|---------|-----|
| `CommunityToolkit.Mvvm` | 8.3.2 | MVVM, ObservableObject, RelayCommand |
| `Microsoft.Extensions.DependencyInjection` | 8.0.1 | IoC Container |
| `Newtonsoft.Json` | 13.0.3 | Serialización JSON |

yt-dlp y ffmpeg **no** son dependencias NuGet: la app llama directamente al binario `yt-dlp.exe` vía `Process` (sin wrapper), y lo descarga junto con `ffmpeg.exe`/`ffprobe.exe` desde GitHub en el primer arranque.

---
