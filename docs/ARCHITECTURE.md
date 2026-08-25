# Architecture decisions

Wynil uses a layered, event-driven design. The WPF composition root creates long-lived services and disposes them in reverse order. Media events produce immutable `MediaTrack` snapshots. A later bridge will version and serialize those snapshots to the WebView without exposing native objects.

The wallpaper host and media service are separate so Lively mode can run the same frontend with only the local media companion. WorkerW integration is isolated behind `IWallpaperHost`; no Explorer handle leaks into view models. User configuration is JSON and migrations will be versioned when the schema expands.

Failure policy is conservative: an Explorer or WebView failure stops wallpaper rendering and detaches owned windows; media failures fall back to an idle record; malformed browser messages are rejected before reaching the unified model.
