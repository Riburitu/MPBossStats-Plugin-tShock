# MPBossStats

**English** · **[Español](#español)**

# English

TShock plugin that records **per-player statistics of boss fights** — damage dealt, damage taken, deaths, time alive and DPS — and shows them **to everyone** when the fight ends. It is a TShock take on the tModLoader mod *MPBossStats*, with reports in **English and Spanish** (each player picks their own language).

- **Compatible with:** Terraria 1.4.5.8 · TShock 6.2.1 (.NET 9) · plugin API 2.1
- **Status:** finished and tested by the author on Terraria 1.4.5.8 / TShock 6.2.1. The compiled `MPBossStats.dll` is published on the project's *Releases* page.
- **License:** [GNU GPL v3](LICENSE) — you may use, modify and share it, but any modified version you distribute must also be open source under the same license, so improvements come back to everyone.

## Installation
1. Download `MPBossStats.dll` from *Releases*.
2. Copy it to your server's `ServerPlugins` folder.
3. Restart the server. `MPBossStats.json` is created in the `tshock` folder.

## Commands
Alias: `/bs` (for `/bossstats`).

| Command | Permission | What it does |
|---|---|---|
| `/bossstats` | none | Shows whether the plugin is ON or OFF and whether a fight is in progress. |
| `/bossstats show` | none | Shows the current fight report, or the last finished one. |
| `/bossstats on` / `off` | `bossstats.toggle` | Turns the plugin on or off (announced to all players). |
| `/bossstats reset` | `bossstats.toggle` | Clears the recorded statistics. |
| `/bosslang en\|es\|auto` | none | Sets **your** language for the messages and reports. `auto` follows the server language. Without arguments it shows your current language. |

Give the permission to a group with, for example: `/group addperm admin bossstats.toggle`.

## Language
Every player sees the messages and fight reports in their own language. By default it follows the server (`Language` = `auto`), and each player can change theirs with `/bosslang`. Player choices are saved in `tshock/MPBossStatsPlayers.json` (per TShock account, or name + IP for players who are not logged in). The console always uses the server language.

## Configuration (`tshock/MPBossStats.json`)
| Option | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Plugin on/off. |
| `EndGraceTicks` | `120` | Ticks (60 = 1 second) with no living boss before the fight is considered over. |
| `Language` | `"auto"` | Default language: `"auto"` (the server's game language), `"en"` or `"es"`. |

## Building from source
Install the .NET 9 SDK, create a `References` folder and copy `TShockAPI.dll`, `OTAPI.dll`, `TerrariaServer.dll` and `ModFramework.dll` from your TShock into it (do not redistribute them), then run `Compilar.bat` or `dotnet build -c Release`. The output is `bin/Release/net9.0/MPBossStats.dll`.

## Notes
- If you used an earlier version named *BossStats*, rename `tshock/BossStats.json` to `tshock/MPBossStats.json` to keep your settings, and remove the old `BossStats.dll` from `ServerPlugins`.

## Credits
Author: **Riburitu**. Inspired by the tModLoader mod [*MPBossStats*](https://steamcommunity.com/sharedfiles/filedetails/?id=2822937879).

---

## Español

Plugin de TShock que registra las **estadísticas por jugador de las peleas contra jefes** —daño hecho, daño recibido, muertes, tiempo vivo y DPS— y las muestra **a todos** cuando termina la pelea. Es una versión para TShock del mod de tModLoader *MPBossStats*, con informes en **inglés y español** (cada jugador elige el suyo).

- **Compatible con:** Terraria 1.4.5.8 · TShock 6.2.1 (.NET 9) · API de plugins 2.1
- **Estado:** terminado y probado por el autor en Terraria 1.4.5.8 / TShock 6.2.1. El `MPBossStats.dll` ya compilado se publica en la página de *Releases* del proyecto.
- **Licencia:** [GNU GPL v3](LICENSE) — puede usarlo, modificarlo y compartirlo, pero toda versión modificada que distribuya debe seguir siendo de código abierto con la misma licencia, de modo que las mejoras vuelvan a todos.

## Instalación
1. Descargue `MPBossStats.dll` desde *Releases*.
2. Cópielo a la carpeta `ServerPlugins` de su servidor.
3. Reinicie el servidor. Se crea `MPBossStats.json` en la carpeta `tshock`.

## Comandos
Alias: `/bs` (para `/bossstats`).

| Comando | Permiso | Qué hace |
|---|---|---|
| `/bossstats` | ninguno | Indica si el plugin está en ON u OFF y si hay una pelea en curso. |
| `/bossstats show` | ninguno | Muestra el informe de la pelea actual o el de la última terminada. |
| `/bossstats on` / `off` | `bossstats.toggle` | Activa o desactiva el plugin (se anuncia a todos los jugadores). |
| `/bossstats reset` | `bossstats.toggle` | Borra las estadísticas registradas. |
| `/bosslang en\|es\|auto` | ninguno | Cambia **su** idioma para los mensajes e informes. `auto` sigue el idioma del servidor. Sin argumentos muestra su idioma actual. |

Para dar el permiso a un grupo, por ejemplo: `/group addperm admin bossstats.toggle`.

## Idioma
Cada jugador ve los mensajes y los informes de pelea en su propio idioma. Por defecto sigue al servidor (`Language` = `auto`) y cada jugador puede cambiar el suyo con `/bosslang`. Las elecciones se guardan en `tshock/MPBossStatsPlayers.json` (por cuenta de TShock, o nombre + IP si el jugador no ha iniciado sesión). La consola usa siempre el idioma del servidor.

## Configuración (`tshock/MPBossStats.json`)
| Opción | Por defecto | Significado |
|---|---|---|
| `Enabled` | `true` | Plugin activado/desactivado. |
| `EndGraceTicks` | `120` | Ticks (60 = 1 segundo) sin ningún jefe vivo antes de dar la pelea por terminada. |
| `Language` | `"auto"` | Idioma por defecto: `"auto"` (el idioma del juego en el servidor), `"en"` o `"es"`. |

## Compilar desde el código fuente
Instale el SDK de .NET 9, cree una carpeta `References` y copie en ella `TShockAPI.dll`, `OTAPI.dll`, `TerrariaServer.dll` y `ModFramework.dll` de su TShock (no las redistribuya); después ejecute `Compilar.bat` o `dotnet build -c Release`. El resultado es `bin/Release/net9.0/MPBossStats.dll`.

## Notas
- Si usaba una versión anterior llamada *BossStats*, renombre `tshock/BossStats.json` a `tshock/MPBossStats.json` para conservar su configuración y quite el antiguo `BossStats.dll` de `ServerPlugins`.

## Créditos
Autor: **Riburitu**. Inspirado en el mod de tModLoader [*MPBossStats*](https://steamcommunity.com/sharedfiles/filedetails/?id=2822937879).
