# Calculator

A clean, Apple-style calculator for Windows (C# + Avalonia) that grows into a full scientific calculator as you widen the window.

- **Small window**: digits, + − × ÷, %, ±, brackets, √x, ∛x, ⁿ√x (any root), xʸ, x², and stacked fractions.
- **Wider (≥ 450 px)**: adds π, e, x³, 1/x, ln, log, eˣ, 10ˣ, x!, |x|, EE, S⇔D (fraction ⇄ decimal), Ans, Deg/Rad.
- **Full width (≥ 730 px)**: adds sin/cos/tan and their inverses, hyperbolic functions and inverses, sec/csc/cot, log₂, log with any base, 2ˣ, floor/ceil, Rand, mod, nCr, nPr, and memory (mc, m+, m−, mr).
- Real 2-D math: fractions sit above and below the line, exponents are raised, roots show their index. Use the arrow keys (or ‹ ›) to move between numerator, denominator, root index, etc.
- A live result preview appears while you type. After `=`, typing an operator continues from the answer.
- **Look**: rounded borderless window with red/yellow/green close, minimise and full-screen buttons. Every label, symbol and number is drawn in one font (Segoe UI Variable by default).
- **Appearance** (⚙ or Ctrl + ,): Light/Dark mode, accent colour swatches or a custom hex, custom background and text colour, window opacity and font.
- **Hotkeys**: every command, including each root, power and function, can get its own shortcut in Settings › Hotkeys.

Digits, `+ - * / ^ ( ) ! % =` and `.` / `,` can always be typed. Some default shortcuts: `F` fraction, `R` √, `Shift+R` ∛, `Ctrl+R` ⁿ√, `Y` xʸ, `Q` x², `S/O/T` sin/cos/tan, `N` ln, `G` log, `L` log with base, `P` π, `E` e, `Enter` =, `Esc` clear, `Ctrl+C` copy result.

Settings are saved in `%APPDATA%\Calculator\settings.json`.

## Build

```bash
dotnet run -c Release
```

Build the portable exe and the installer (`publish\Calculator.exe` and `publish\CalculatorSetup.exe`):

```bash
powershell -ExecutionPolicy Bypass -File build.ps1
```

The setup exe installs per user to `%LOCALAPPDATA%\Programs\Calculator` (no admin rights), adds a Start Menu entry (and an optional desktop shortcut), and registers an uninstaller under Settings › Apps. `CalculatorSetup.exe --install` installs silently.

The icon is drawn by `tools\make-icon.ps1`.
