# WindowTags

> App Windows que fixa rótulos flutuantes sobre janelas de navegador. O rótulo segue a janela alvo ao mover, minimizar/restaurar ou fechar.

## Visão geral

Para quem trabalha com muitas janelas de Chrome/Edge/Firefox abertas em paralelo (cliente A, cliente B, dev, prod, etc.) e perde o controle de qual é qual, o WindowTags cola um rótulo visual em cima da janela. O rótulo fica sempre visível, sempre ligado àquela janela específica, e some quando a janela some.

A "cola" entre rótulo e janela é via **owner relationship** do Windows (`SetWindowLongPtr` com `GWLP_HWNDPARENT`), não `SetParent`. Isso dá z-order automático, minimiza junto, e funciona cross-process — inclusive em apps Chromium, que são hostis a `SetParent`.

## Recursos

- Rótulos arrastáveis dentro da janela alvo, com offset relativo persistente
- Texto que escala com o rótulo via `Viewbox` (não precisa calcular `FontSize`)
- Resize por grip no canto inferior direito
- DPI Per-Monitor V2 — funciona em setups com monitores de escalas diferentes
- Hotkeys globais para adicionar/editar e remover sem sair do navegador
- Tray icon com menu de atalhos
- Bloqueio automático de `Win+Setas` e maximize que destruiriam a posição do rótulo
- Detecção automática de browsers por classe Win32 (`Chrome_WidgetWin_1`, `MozillaWindowClass`)

## Atalhos globais

| Combo | Ação |
|-------|------|
| `Ctrl+Alt+L` | Adicionar/editar rótulo no navegador em foco |
| `Ctrl+Alt+R` | Remover rótulo |

## Como usar (binário)

1. **Pré-requisito**: instale o [.NET 9 Desktop Runtime (x64)](https://aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe) na máquina alvo.
2. Baixe o `WindowCards.exe` da raiz deste repositório.
3. Execute. O ícone aparece no system tray.
4. Coloque foco em uma janela de Chrome/Edge/Firefox e pressione `Ctrl+Alt+L`.
5. Digite o texto do rótulo. Ele aparece grudado ao topo-esquerda da janela.
6. Arraste para reposicionar. Use o grip do canto para redimensionar.

O exe é **framework-dependent** (~250 KB). Sem o runtime instalado, o próprio Windows abre uma caixa de diálogo oferecendo o download — não trava silenciosamente.

## Build a partir do código

```powershell
# build debug
dotnet build WindowCards.sln

# rodar (matar versão anterior antes — exe fica bloqueado durante build)
Get-Process WindowCards -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process "src\WindowCards.App\bin\Debug\net9.0-windows\WindowCards.exe"

# publicar exe single-file framework-dependent
dotnet publish src\WindowCards.App\WindowCards.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false -o publish
```

Se `dotnet --version` falhar na sua sessão, recarregue o PATH:

```powershell
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

## Estrutura

```
WindowCards.sln
├── src/WindowCards.App     — Host WPF (CardWindow, dialogs, hotkeys, tray)
├── src/WindowCards.Core    — Win32 interop + tracking (sem deps de WPF)
└── src/WindowCards.Models  — POCOs (CardConfig, Rule, AppSettings)
```

`App` referencia `Core` e `Models`. `Core` referencia `Models`. Não inverter.

## Stack

- .NET 9 SDK (`9.0.313`+), WPF, C# 12, `Nullable=enable`
- TFM `net9.0-windows` nos três projetos (**NÃO** `net9.0` puro — quebra WPF e CsWin32)
- [CsWin32](https://github.com/microsoft/CsWin32) `0.3.275` — source generator de P/Invoke

## Decisões arquiteturais

- **Top-level + owner**, não `SetParent` cross-process. `SetParent` quebra em Chromium e tem problemas com UAC.
- **Geometria via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`**, não `GetWindowRect` — este último retorna a região com a sombra invisível do DWM e desalinha o rótulo.
- **Estilos do rótulo**: `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED`, aplicados em `OnSourceInitialized`. `NOACTIVATE` é o que impede o rótulo de roubar foco do navegador ao ser clicado.
- **WndProc** bloqueia `SC_MOVE`, `SC_MAXIMIZE`, `SC_MINIMIZE`, `SC_RESTORE` mas **mantém** `SC_SIZE` (senão o resize via grip quebra). `WM_MOUSEACTIVATE` retorna `MA_NOACTIVATE` — defesa real contra `Win+Setas`.
- **Coordenadas em pixels físicos** durante drag (via `GetCursorPos`), não DIPs do WPF — evita jitter ao atravessar monitores com escalas diferentes.

Mais detalhes técnicos e pegadinhas conhecidas em [`CLAUDE.md`](CLAUDE.md).

## Status

**MVP funcional.** Já entregue:

- Criar/editar/remover rótulos via hotkey global e menu de contexto
- Drag livre dentro da janela alvo com persistência do offset
- Owner relationship para minimize/z-order automáticos
- DPI Per-Monitor V2
- Resize com grip + texto que escala
- Tray icon com `NotifyIcon`
- Bloqueio de `Win+Setas`/maximize via `WM_SYSCOMMAND` + `MA_NOACTIVATE`
- Exe single-file framework-dependent

Roadmap (em ordem):

- Persistência JSON de regras em `%APPDATA%`
- Picker visual com cursor target
- Detecção de fullscreen
- Suporte a virtual desktops
- Throttle de `EVENT_OBJECT_LOCATIONCHANGE`
- Instalador (Inno Setup ou MSIX)

## Notas

O repositório se chama **WindowTags** mas o código-fonte e o binário ainda usam a nomenclatura legada `WindowCards` (`<AssemblyName>`, `WindowCards.sln`, `CardWindow.xaml`, etc.). Renomear o código todo é uma decisão para uma versão futura.

## Licença

A definir.
