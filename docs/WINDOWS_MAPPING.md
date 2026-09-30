# FaceGate-Mac → Windows mapping (Phase 0)

Reference studied: `FaceGate-Mac` @ main (Swift/SwiftUI). This is a behavioural reference, not code to copy.

## Component map

| FaceGate-Mac file | Job | Windows counterpart | Status |
|---|---|---|---|
| `Core/AppMonitor.swift` | NSWorkspace launch/activate/deactivate notifications | `ProcessWatcher` + foreground-window hook (Infrastructure) feeding `AppLockEngine` | **Engine done**, watcher TODO |
| `Core/AppLocker.swift` | hide app + overlay on every screen; terminate on cancel | Blocking strategy (see below) + `AuthWindow` per monitor | TODO |
| `Core/SessionManager.swift` | per-app unlock sessions, focus-mode timer | `AppLock/SessionManager.cs` | **Done + tested** |
| `Core/LockedAppsManager.swift`, `Models/LockedApp.swift` | protected list | `Models/ProtectedApp.cs`, `ProtectedAppRegistry` | **Done** |
| `Utilities/Constants.swift` (UserDefaults keys) | settings | `Configuration/AppConfig.cs` + `ConfigStore` (JSON, atomic write) | **Done** |
| `Auth/PasswordAuth.swift` | password/PIN | `Security/PinHasher`, `AttemptLimiter`, `PinAuthenticator` | **Done + tested** |
| `Utilities/KeychainHelper.swift` | secret storage | `IPinStore` → DPAPI / Credential Manager impl | interface only (Phase 8) |
| `Utilities/CryptoHelper.swift` | AES-256-GCM | `System.Security.Cryptography.AesGcm` | Phase 8 |
| `Auth/TouchIDAuth.swift` | Touch ID | Windows Hello (`UserConsentVerifier`) | Phase 3/v2 |
| `FaceAuth/CameraManager.swift` | AVFoundation capture | Media Foundation / WinRT `MediaCapture` | Phase 4 |
| `FaceAuth/FaceDetector/FaceEmbedder.swift` | Vision + Core ML | ONNX Runtime detector + embedder | Phase 5–6 |
| `FaceAuth/FaceMatcher.swift`, `Utilities/VectorMath.swift` | cosine similarity vs enrolled set (default 0.65) | pure C#, port when embeddings exist | Phase 6 |
| `Core/AppScheduleManager.swift` | lock/unlock time windows | `Scheduling/` (timer-based, no busy loop) | Phase 9 |
| `AppDelegate.swift` sleep/lock observers | lock-on-sleep | `SystemEvents.SessionSwitch` / power events → `AppLockEngine.LockAll()` | Phase 9 (engine hook exists) |
| `GlobalHotkeyManager.swift` | emergency kill | `RegisterHotKey` | v2 |
| menu bar UI | tray | `NotifyIcon`-style tray | Phase 1 |

## Decisions where Windows differs from the Mac app

1. **Blocking a launched app.** macOS can `hide()` an app instantly. Windows has no equivalent. Options:
   - **A (recommended for MVP):** minimize the app's top-level windows and show a topmost full-screen auth window; on Cancel, terminate the process (Mac does this too). Uses documented Win32 APIs only.
   - **B:** suspend the process while the auth window is up. Stronger (the app can't run behind the overlay) but relies on `NtSuspendProcess`, which isn't a documented Win32 API — verify before adopting.
   - The app will be briefly visible before it is covered on Windows. That's inherent to the user-mode approach; document it.
2. **Detecting launches without admin.** As far as I know, the WMI `Win32_ProcessStartTrace` event needs an elevated process, which conflicts with plan §8/§30 (least privilege). Proposed: lightweight 500 ms–1 s process-list diff for launches (near-zero CPU) plus `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` for focus/blur (no admin). Confirm on your machine.
3. **Clock.** Sessions use monotonic `TimeProvider` timestamps, so changing the Windows clock can't extend a session (Mac uses wall-clock `Date`).
4. **PIN hashing.** Mac: SHA-256 + salt. Windows: PBKDF2-HMAC-SHA-256, 600 000 iterations, per plan §15.
5. **Lockout.** Mac: 5 failures → 60 s. Windows: 5 free failures, then 30 s doubling to a 15 min cap. In-memory only for now (restart resets it — Phase 10).
6. **Config fails closed.** A corrupt `config.json` raises an error; it never silently means "nothing protected".
7. **Name matching fails closed.** Rules match on exe file name only; a stored path never *narrows* a match, so copying `chrome.exe` to another folder doesn't evade the lock. (Renaming the exe still would; file-hash matching is planned.)

## Upstream lessons to design around (plan §47)
Camera/auth lifecycle races and thread-safety bugs, and image-orientation issues in the embedding pipeline. Already applied: every shared state class here uses a lock and has a concurrency test. Still to do: single-owner camera session with cancellation, and orientation tests with fixture frames.
