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
| Disable Windows Defender | various | Never part of a level or a normal tweak: it removes the only antivirus most people have. It is only offered in the "At your own risk" zone, with the steps Windows requires (Tamper Protection off by hand) and a way back. |
| Remove Microsoft Edge | various | Never part of a level or a normal tweak: some apps and Windows features rely on it. It is only offered in the "At your own risk" zone with the official uninstaller; WebView2 is kept. |
| Disable TabletInputService | `TabletInputService` | It breaks the emoji panel, the touch keyboard and some input methods. |
| Global "disable fullscreen optimizations" | `GameDVR_FSEBehaviorMode` | It blocks the modern flip model and often increases latency on Windows 11. |
| Mouse and keyboard buffer size | `MouseDataQueueSize`, `KeyboardDataQueueSize` | Popular on TikTok and forums, but there is no controlled measurement of any gain; games that use raw input bypass this buffer, and too small a value drops input. |
| Timer resolution, HPET, `useplatformclock` | `bcdedit /set useplatformclock`, timer tools | Results are contradictory and often worse latency; Windows 11 already manages timer resolution per process. |
| Disable Spectre and Meltdown mitigations | `FeatureSettingsOverride` | A small gain on old CPUs for a serious security hole. |
| "Free RAM" tools and tweaks | various | Windows uses free RAM as cache on purpose; emptying it makes the next load slower. |
| Disable IPv6 | `DisabledComponents` | Microsoft does not support it; it breaks some Windows features and does not speed up the connection. |

