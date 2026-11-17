# Tweaks Optimizzzer leaves out on purpose

Other optimizers apply the changes below. Optimizzzer does not, because there is no reliable evidence that they help on Windows 10/11, or because they cause real harm. If good evidence appears, a tweak can be added with its source.

| Tweak | Registry / command | Why it is excluded |
|---|---|---|
| Svchost split threshold | `SvcHostSplitThresholdInKB` | It only groups services into fewer processes. Memory use barely changes, and a crash in one service then takes down the others. |
| Large system cache | `LargeSystemCache` | This is a server setting. On desktops it can cause stutter, and it was known to corrupt data with some drivers. |
| Network throttling index | `NetworkThrottlingIndex` | It affects multimedia scheduling on old Windows versions. It does not raise FPS or lower ping. |
| MMCSS system responsiveness | `SystemResponsiveness` | It is often set to 0, which Windows clamps to 10 anyway. There are no reproducible gains. |
| Nagle / TCP ACK | `TcpAckFrequency`, `TCPNoDelay` | Modern games already disable Nagle on their own sockets. A global change can hurt other traffic. |
| Priority separation | `Win32PrioritySeparation` | Results are contradictory, the claims are unmeasured, and the default is already tuned for foreground apps. |
| Disable paging executive | `DisablePagingExecutive` | No measurable effect on systems with enough RAM. |
| QoS 20% reserved bandwidth | `NonBestEffortLimit` | It is a myth: Windows does not reserve bandwidth unless a QoS application asks for it. |
| Disable SysMain (Superfetch) | `SysMain` service | On SSDs the effect is negligible. On HDDs it usually makes app launches slower. |
| Disable Prefetch | `EnablePrefetcher` | Slower app and boot times, especially on HDDs. |
| Disable memory compression | `Disable-MMAgent -MemoryCompression` | More paging to disk on PCs with little RAM. |
| Disable Windows Update service | `wuauserv` | It leaves the PC without security patches. Optimizzzer offers safe controls instead (no auto-restart, stay on a version). |
| Disable Windows Defender | various | It removes the only antivirus most people have. |
| Remove Microsoft Edge | various | It breaks WebView2 apps and Windows features, and Windows reinstalls it. |
| Disable TabletInputService | `TabletInputService` | It breaks the emoji panel, the touch keyboard and some input methods. |
| Global "disable fullscreen optimizations" | `GameDVR_FSEBehaviorMode` | It blocks the modern flip model and often increases latency on Windows 11. |
