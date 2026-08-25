# Installer

Install Inno Setup 6 and run `powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Installer`. The script builds the frontend, tests the solution, creates a self-contained x64 publish, then writes the signed-ready installer to `artifacts/installer`.

WebView2 Evergreen is downloaded and installed only when its runtime registration is missing. Startup and shortcut creation are optional. The uninstall data-retention choice is recorded during setup.
