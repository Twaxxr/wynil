# Known Windows limitations

WorkerW wallpaper hosting depends on an undocumented Explorer hierarchy. NowSpinning owns only its WPF windows, always closes them during normal shutdown, and never modifies Explorer windows. If Explorer restarts, pause and resume wallpaper mode to rediscover WorkerW.

Windows media metadata is advisory. Source applications decide which fields and commands are exposed. Browser fallback exists for pages that do not publish useful GSMTC data, but browser security still prevents access to protected media details.

The current native host creates one WebView2 scene per monitor. Display additions, removals, rotations, or DPI changes while it is running take effect after wallpaper mode is restarted. Spanning is represented in configuration, while the production default intentionally preserves one independently scaled scene per monitor.

Fullscreen and battery behavior is exposed in configuration. Windows does not provide one authoritative definition of a fullscreen game, and protected/anti-cheat processes cannot safely be inspected, so conservative user control remains available through the tray.

The Inno installer is reproducible but unsigned. Production distribution should sign both the application and installer with an Authenticode certificate.
