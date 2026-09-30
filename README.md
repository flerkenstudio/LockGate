# FaceGate for Windows

Windows-native application locker inspired by [FaceGate-Mac](https://github.com/dweep-desai/FaceGate-Mac).
Reimplementation in C#/.NET, not a translation. See `WINDOWS_PORT_PLAN.md` for the full plan and `docs/WINDOWS_MAPPING.md` for the Mac→Windows mapping.

## Status — Milestone 1, step 1 (platform-neutral core)

| Piece | State |
|---|---|
| `FaceGate.Core` — session timers, lock engine, protected-app registry | done, 43 tests passing |
| `FaceGate.Core` — PIN verifier (PBKDF2), throttling, JSON config | done, tested |
| Windows process watcher, auth window, tray (WinUI 3) | **next** — needs Windows to build |
| Camera, face AI, Windows Hello, DPAPI storage | later phases |

Nothing here talks to a camera or handles biometric data yet.

## Build & test

```powershell
dotnet build FaceGate.sln
dotnet run --project tests/FaceGate.Core.Tests
```

The test project is a tiny dependency-free runner (written where NuGet was unreachable). On your machine, convert it to xunit if you prefer — the test bodies use plain assertions.

## Security boundary

An application locker, not a replacement for Windows sign-in, BitLocker or Windows Hello. A local administrator or malware can interfere with any user-mode locker. The PIN verifier is only as private as the storage it sits in (DPAPI/Credential Manager arrives in Phase 8), and a short PIN is weak against offline guessing regardless of the hash.

## License / attribution

No FaceGate-Mac source or assets are copied; behaviour was studied as a reference. Upstream is MIT-licensed — keep its notice if you later copy anything. Add your own `LICENSE` before publishing.
