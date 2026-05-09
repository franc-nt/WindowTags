# WindowCards

App Windows que fixa cards (rótulos vermelhos) sobre qualquer janela top-level (navegadores, editores, file explorer, UWP, etc.). O card segue a janela alvo: mover, minimizar/restaurar, fechar. Plano de desenvolvimento completo em [`C:\Users\Francisco\.claude\plans\fa-a-uma-analise-do-wise-wombat.md`](C:/Users/Francisco/.claude/plans/fa-a-uma-analise-do-wise-wombat.md).

## Stack

- .NET 9 SDK (`9.0.313`), WPF, C# 12, `Nullable=enable`
- TFM: **`net9.0-windows`** nos 3 projetos (App, Core, Models). NÃO usar `net9.0` puro — quebra o WPF e o CsWin32.
- CsWin32 `0.3.275` (source-generator de P/Invoke)

## Layout

```
WindowCards.sln
├── src/WindowCards.App           — WPF host (CardWindow, dialogs, hotkeys)
├── src/WindowCards.Core          — Win32 interop + tracking (sem deps de WPF)
└── src/WindowCards.Models        — POCOs (CardConfig, Rule)
```

App referencia Core e Models. Core referencia Models. Não inverter.

## Comandos

Assembly renomeado para `WindowCards` (via `<AssemblyName>` no csproj). `Get-Process` usa `WindowCards`, não `WindowCards.App`.

```powershell
# build debug
dotnet build WindowCards.sln

# rodar debug (matar versão anterior antes — exe fica bloqueado durante build)
Get-Process WindowCards -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Process "src\WindowCards.App\bin\Debug\net9.0-windows\WindowCards.exe"

# publicar exe framework-dependent na raiz (c:\Apps\cards\WindowCards.exe, ~250 KB)
# requer .NET 9 Desktop Runtime (x64) instalado na máquina alvo
dotnet publish src\WindowCards.App\WindowCards.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false -o publish
Move-Item -Force publish\WindowCards.exe WindowCards.exe
Remove-Item -Recurse -Force publish

# encerrar
Get-Process WindowCards | Stop-Process -Force
```

`dotnet` pode não estar no PATH da sessão atual; se `dotnet --version` falhar, recarregar:
```powershell
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
```

## Hotkeys já registradas (não reusar)

- `Ctrl+Alt+L` — adicionar/editar card no navegador em foco
- `Ctrl+Alt+R` — remover card

## Decisões arquiteturais que NÃO podem ser revertidas sem discussão

1. **Card é janela top-level independente**, NÃO `SetParent` cross-process. SetParent quebra em apps Chromium e tem problemas de UAC. A "cola" entre card e alvo é via **owner relationship** (`SetWindowLongPtr` com `GWLP_HWNDPARENT`). Isso dá minimize-junto e z-order acima do owner automaticamente, cross-process.

2. **Geometria via `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`**, não `GetWindowRect`. `GetWindowRect` retorna a região com a sombra invisível DWM e desalinha o card. Helper: `WindowGeometry.GetExtendedFrameBounds(hwnd)`. Para a janela do próprio card (sem sombra DWM), `WindowGeometry.GetWindowRectBounds(hwnd)`.

3. **Estilos do card**: `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED`. Aplicados em `OnSourceInitialized` via `CardWindowStyler.ApplyOverlayStyles`. Não remover NOACTIVATE — é o que impede o card de roubar foco do navegador ao clicar.

4. **DPI Per-Monitor V2** via `app.manifest`. Coordenadas em `SetWindowPos` são pixels físicos; converter de DIPs WPF multiplicando por `VisualTreeHelper.GetDpi(this).DpiScaleX/Y`.

5. **Texto que escala com o card** = `Viewbox` no XAML envolvendo o `TextBlock`. Não calcular FontSize manualmente.

6. **WndProc bloqueia gerenciamento de janela**: o card intercepta `WM_SYSCOMMAND` em `CardWindow.WndProc` e descarta `SC_MOVE`, `SC_MAXIMIZE`, `SC_MINIMIZE`, `SC_RESTORE`. **NÃO incluir `SC_SIZE`** — o grip de resize do mouse chega como `WM_SYSCOMMAND` `SC_SIZE | HTBOTTOMRIGHT` e seria quebrado. Também responde `WM_MOUSEACTIVATE` com `MA_NOACTIVATE` para o card jamais virar foreground ao ser clicado. Win+Setas/Win+Up só agem em janela foreground, então `MA_NOACTIVATE` é a defesa real contra snap; o filtro de `SC_*` é redundância para casos raros.

## Convenções

- **Manual P/Invoke** quando CsWin32 não gera (ex: `SetWindowLongPtr`): usar `EntryPoint = "...W"` e `CharSet = CharSet.Unicode`. Exemplo em `CardWindowStyler.cs`.
- **NativeMethods.txt** lista nomes de **enum** (`SET_WINDOW_POS_FLAGS`, `WINDOW_EX_STYLE`), não constantes individuais. CsWin32 emite warning `PInvoke004` se você listar `SWP_NOACTIVATE` em vez de `SET_WINDOW_POS_FLAGS`.
- **`SetWinEventHook` retorna `UnhookWinEventSafeHandle`**, não `IntPtr`. Tratar como SafeHandle (Dispose limpa o hook).
- **Eventos WinEvent**: registrar com `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`. `idProcess=0, idThread=0` recebe eventos de toda a área de trabalho.
- **Throttling**: `EVENT_OBJECT_LOCATIONCHANGE` dispara dezenas de vezes/seg durante drag. Coalescer com `DispatcherTimer 16ms` ANTES de implementar features pesadas (atualmente sem throttle — adicionar quando virar problema).
- **Durante drag do card**, `OnTargetBoundsChanged` deve sair via `if (_dragging) return;` — senão o tracker reposiciona o card por baixo do mouse.
- **Detecção de janela alvo** (`TargetWindowDetector.TryClassify`): aceita qualquer top-level com `IsWindowVisible == true`, `GW_OWNER == 0`, título não vazio, tamanho não-zero, e que não seja do próprio processo. Rejeita classes da shell: `Progman`, `WorkerW`, `Shell_TrayWnd`, `Shell_SecondaryTrayWnd`, `TaskListThumbnailWnd`, `MultitaskingViewFrame`, `ForegroundStaging`, `ApplicationManager_DesktopShellWindow`. **Não** filtra mais por nome de processo — qualquer aplicativo é elegível.
- **Hotkey global**: janela message-only criada via `HwndSource` com `ParentWindow = HWND_MESSAGE (-3)`. WPF não dá API direta, daí o `HotkeyHost.cs`.

## WPF + WinForms juntos (tray)

`<UseWindowsForms>true</UseWindowsForms>` está ativo para usar `NotifyIcon` (caminho mais leve para tray em WPF). Isso causa **ambiguidades de tipo** entre os dois namespaces. Resolvido via `GlobalUsings.cs` que aponta `Application`, `MessageBox`, `MouseEventArgs`, `MouseButtonEventArgs`, `KeyEventArgs` para os tipos do WPF. `Color`/`ColorConverter`/`Brushes` ficam **sem alias global** — quem precisa do tipo do System.Drawing (ex: `TrayIconHost` desenhando bitmap do ícone) usa o namespace local; quem precisa do WPF (ex: `CardWindow.ApplyColors`) qualifica com `System.Windows.Media.Color`. Não adicionar alias global para esses três — colidem com o uso local em `TrayIconHost`.

`<NoWarn>WFO0003</NoWarn>` suprime o aviso do WinForms pedindo para mover DPI awareness do `app.manifest` para `ApplicationHighDpiMode` — ignorar; o app é principalmente WPF e o manifest é o caminho correto.

## Pegadinhas conhecidas

- **UAC**: se navegador rodar elevado e o app não, `SetWinEventHook` não recebe eventos daquela janela. Manifest está em `asInvoker` propositalmente — não mudar para `requireAdministrator`.
- **`Dispatcher.BeginInvoke(Close)` e `BeginInvoke(Shutdown)`** falham com CS1503 (method group ambíguo). Embrulhar em `new Action(Close)` ou lambda.
- **Resize grip vs drag**: `ResizeMode="CanResizeWithGrip"` adiciona um grip que intercepta WM_NCHITTEST antes dos handlers do `Border`. Não há conflito; manter assim.
- **`PositionOverTarget` lê `ActualWidth/ActualHeight` (DIPs reais)**, não `_config.Width/Height`. Se ler do `_config`, o resize do usuário pelo grip é descartado quando o navegador minimiza/restaura — o card "encolhe" para o tamanho original. O método também atualiza `_config` no fim para persistir o tamanho corrente.
- **Drag do card usa `GetCursorPos` (P/Invoke manual no `CardWindow`)**, não `PointToScreen` da WPF. Mantém coordenadas em pixels físicos durante o drag inteiro, evitando jitter por conversão DPI quando se move entre monitores com escalas diferentes.
- **UWP / apps modernas** (Calculator, Settings, Notepad novo, etc.): a janela visível costuma ser `ApplicationFrameWindow` hospedando uma `Windows.UI.Core.CoreWindow` interna. Owner relationship funciona, mas o processo da app pode ser **suspenso** quando minimizado/em background — `EVENT_OBJECT_LOCATIONCHANGE` pode parar até a app reativar. Não é bug do WindowCards, é comportamento do PLM. Move/restore voltam a disparar quando a app volta ao foco.
- **Apps com chrome customizado** (Discord, Spotify, VS Code): o DWM pode reportar `DWMWA_EXTENDED_FRAME_BOUNDS` levemente diferente do esperado. Fallback para `GetWindowRect` já existe em `WindowGeometry`. Se o card desalinhar em algum app específico, investigar caso-a-caso.
- **Não criar arquivos .md** sem o usuário pedir explicitamente. Esta CLAUDE.md foi pedida.

## Artefato distribuível

`c:\Apps\cards\WindowCards.exe` é o exe single-file **framework-dependent** (~250 KB). Pré-requisito na máquina alvo: **.NET 9 Desktop Runtime (x64)** instalado (download: aka.ms/dotnet/9.0/windowsdesktop-runtime-win-x64.exe). Sem o runtime instalado, o exe abre uma caixa de diálogo do próprio Windows oferecendo o download — não trava silenciosamente.

Por que não self-contained: a versão self-contained tem ~170 MB porque embute o runtime + WPF + WinForms inteiros. Trimming não é viável (WPF usa reflection no XAML e quebra em runtime). Decisão consciente trocando 170 MB por uma instalação única de runtime do lado do usuário.

**SEMPRE republicar o exe da raiz após qualquer mudança de código antes de encerrar a tarefa.** O usuário usa esse arquivo direto — deixar só o build Debug atualizado é entrega incompleta. Sequência obrigatória ao terminar qualquer fix/feature:

```powershell
Get-Process WindowCards -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish src\WindowCards.App\WindowCards.App.csproj -c Release -r win-x64 `
  --self-contained false -p:PublishSingleFile=true `
  -p:DebugType=None -p:DebugSymbols=false -o publish
Move-Item -Force publish\WindowCards.exe WindowCards.exe
Remove-Item -Recurse -Force publish
```

Durante iteração ativa (testar várias vezes a mesma mudança), pode-se rodar só o `dotnet build` + Debug exe para velocidade — mas a publicação Release na raiz precisa ser feita antes de reportar a tarefa como concluída.

## Estado do MVP

Implementado: criar/editar/remover card via hotkey global e menu de contexto sobre **qualquer janela top-level**; drag livre dentro da janela alvo com persistência do offset relativo; owner relationship para minimize/z-order automáticos; DPI Per-Monitor V2; resize com grip e texto que escala via Viewbox; tray icon com `NotifyIcon` (clique → `InfoWindow` com atalhos, menu → Mostrar atalhos / Sair); bloqueio de Win+Setas/maximize via `WM_SYSCOMMAND` + `MA_NOACTIVATE`; exe single-file self-contained na raiz.

Faltando (em ordem): persistência JSON de regras em `%APPDATA%`, picker visual com cursor target, fullscreen detection, virtual desktops, throttle de eventos, instalador (Inno/MSIX).
