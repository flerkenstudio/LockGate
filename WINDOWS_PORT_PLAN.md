# WINDOWS_PORT_PLAN.md

# FaceGate Windows — Port & Build Plan

> A Windows-native reimplementation of the FaceGate-Mac concept, optimized for this development laptop:
>
> **Laptop:** OMEN Laptop 15-en0xxx  
> **CPU:** AMD Ryzen 5 4600H, 6 cores / 12 threads, 3.0 GHz base  
> **RAM:** 8 GB (7.36 GB usable)  
> **Graphics:** Radeon Graphics shown by Windows  
> **OS:** Windows 11 Home Single Language, 26H2  
> **Architecture:** 64-bit x64

---

## 1. Project Goal

Build a Windows-native application locker inspired by the functionality of FaceGate-Mac.

The application should allow the user to protect selected Windows applications and authenticate locally using:

1. Face recognition
2. Windows Hello where available
3. PIN/password fallback
4. Liveness / anti-spoofing checks
5. Automatic locking
6. Per-application session timers
7. Lock on Windows lock/sleep
8. Scheduled protection
9. Local encrypted biometric storage
10. System-tray management
11. Tamper-resistant configuration

This is a **reimplementation**, not a line-by-line Swift-to-C# translation.

The original FaceGate repository is a macOS application using Swift/SwiftUI, Core ML, macOS camera/security APIs, Keychain, process management, and menu-bar functionality. Its README documents app locking, on-device face recognition, multi-face enrollment, liveness, external cameras, Touch ID/password fallback, timers, scheduled locking, sleep locking, secure operations and encrypted local storage. See the original repository for reference:
https://github.com/dweep-desai/FaceGate-Mac

The repository is MIT licensed, but the Windows implementation should still preserve license/attribution requirements when reusing original code, assets, or other copyrighted material.

---

# 2. IMPORTANT: Laptop Constraints

This project must be designed around the actual development machine.

## Hardware

| Component | Available |
|---|---|
| CPU | AMD Ryzen 5 4600H |
| CPU cores | 6 cores / 12 threads |
| RAM | 8 GB |
| GPU | AMD Radeon Graphics reported by Windows |
| OS | Windows 11 Home Single Language |
| Architecture | x64 |
| Touch | None |
| Camera | Not confirmed; detect at runtime |

## Main constraint

**8 GB RAM is the biggest development constraint.**

Do NOT design the development workflow around:

- large local LLMs
- heavyweight Docker stacks
- multiple virtual machines
- huge AI models
- running Visual Studio + Android Studio + Docker + browser + local AI simultaneously
- GPU-heavy model training

The application itself should be lightweight.

### Target runtime budget

Aim for:

- Idle memory: roughly 100–250 MB where practical
- Authentication-time memory: preferably below 500 MB
- CPU usage near zero when idle
- Camera/model loaded only when authentication is required
- No continuous face-recognition inference when no protected app needs authentication
- No unnecessary background database polling

These are engineering targets, not guaranteed specifications.

---

# 3. Recommended Technology Stack

## Primary stack

### UI

**C# + .NET + WinUI 3**

Why:

- Native Windows experience
- Modern Windows UI
- Good integration with Windows APIs
- Strong async support
- Good ecosystem
- Easier long-term Windows maintenance than attempting to port SwiftUI

Alternative:

**WPF**

Use WPF if WinUI 3 causes compatibility/development problems on this laptop.

Do not build the first version with Electron.

---

## Backend / Core

```text
C#
.NET
Windows APIs
Task-based async
Dependency Injection where useful
MVVM
```

---

## AI / Computer Vision

Use:

```text
ONNX Runtime
OpenCV
ONNX face detection model
ONNX face embedding model
ONNX liveness model
```

Do not train a face model on this laptop.

Use pretrained models and optimize inference.

Possible architecture:

```text
Camera Frame
    ↓
Resize / Normalize
    ↓
Face Detector
    ↓
Face Crop + Alignment
    ↓
Face Embedding Model
    ↓
Embedding Vector
    ↓
Similarity Comparison
    ↓
Liveness Result
    ↓
Authentication Decision
```

---

# 4. Windows Technology Mapping

| FaceGate-Mac | Windows implementation |
|---|---|
| Swift | C# |
| SwiftUI | WinUI 3 |
| Core ML | ONNX Runtime |
| Apple Neural Engine | ONNX Runtime execution providers / available hardware acceleration |
| Vision | OpenCV + ONNX |
| AVFoundation | Windows camera APIs / Media Foundation |
| Touch ID | Windows Hello |
| macOS Keychain | Windows Credential Manager / DPAPI |
| AES-256-GCM | .NET cryptography |
| Launch Agent | Windows Service / startup mechanism |
| Menu Bar | System Tray / NotifyIcon |
| macOS process APIs | Windows Process APIs |
| App bundle protection | Windows installation + ACLs |
| Sparkle updater | Windows update mechanism / MSIX update |
| DMG | MSIX / installer |
| XcodeGen | .NET solution/project files |

---

# 5. Proposed Project Structure

```text
FaceGate-Windows/
│
├── src/
│   ├── FaceGate.App/
│   │   ├── Views/
│   │   ├── ViewModels/
│   │   ├── Controls/
│   │   ├── Resources/
│   │   ├── Assets/
│   │   └── App.xaml
│   │
│   ├── FaceGate.Core/
│   │   ├── Authentication/
│   │   ├── Face/
│   │   ├── AppLock/
│   │   ├── Scheduling/
│   │   ├── Security/
│   │   ├── Configuration/
│   │   ├── Logging/
│   │   └── Models/
│   │
│   ├── FaceGate.Infrastructure/
│   │   ├── Windows/
│   │   ├── Camera/
│   │   ├── Storage/
│   │   ├── Processes/
│   │   ├── Hello/
│   │   └── Services/
│   │
│   └── FaceGate.AI/
│       ├── Detection/
│       ├── Embeddings/
│       ├── Liveness/
│       ├── Preprocessing/
│       └── Models/
│
├── tests/
│   ├── FaceGate.Core.Tests/
│   ├── FaceGate.AI.Tests/
│   ├── FaceGate.Infrastructure.Tests/
│   └── FaceGate.App.Tests/
│
├── models/
│   ├── face-detector.onnx
│   ├── face-embedding.onnx
│   └── liveness.onnx
│
├── docs/
│   ├── ARCHITECTURE.md
│   ├── SECURITY.md
│   ├── THREAT_MODEL.md
│   ├── AI_PIPELINE.md
│   └── TESTING.md
│
├── scripts/
│   ├── build.ps1
│   ├── test.ps1
│   └── package.ps1
│
├── .github/
│   └── workflows/
│
├── FaceGate.sln
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
├── LICENSE
└── SECURITY.md
```

---

# 6. Core Architecture

```text
                         ┌───────────────────────┐
                         │      WinUI 3 UI       │
                         │ Dashboard / Settings  │
                         └───────────┬───────────┘
                                     │
                                     ▼
                         ┌───────────────────────┐
                         │ Authentication Layer  │
                         └───────────┬───────────┘
                                     │
                    ┌────────────────┼────────────────┐
                    │                │                │
                    ▼                ▼                ▼
              Face Auth        Windows Hello      PIN/Password
                    │
                    ▼
             Face Pipeline
                    │
       ┌────────────┼─────────────┐
       ▼            ▼             ▼
    Camera       Detector      Liveness
       │            │             │
       └────────────┼─────────────┘
                    ▼
               Embeddings
                    │
                    ▼
             Secure Comparator
                    │
                    ▼
             Authentication
                    │
                    ▼
              App Lock Engine
                    │
             ┌──────┴──────┐
             ▼             ▼
        Process Watch   Session Timer
             │             │
             └──────┬──────┘
                    ▼
              Protected App
```

---

# 7. Application Locking Design

The main job is to prevent unauthorized use of selected applications.

## Protected app configuration

Example:

```json
{
  "appId": "chrome",
  "executable": "chrome.exe",
  "enabled": true,
  "lockMode": "on-launch",
  "sessionMinutes": 10,
  "lockWhenFocusLost": false
}
```

Do not rely only on application names.

Use:

- executable path
- executable filename
- process ID
- optional file hash
- installation location

Where possible, verify the executable before applying a rule.

---

# 8. Process Monitoring

Create a lightweight background component:

```text
WindowsAppMonitor
        │
        ├── Detect new process
        ├── Compare against protected apps
        ├── Check current authentication state
        ├── Check session timer
        └── Trigger lock UI if required
```

Avoid aggressive polling.

Preferred design:

- event-driven process detection where practical
- lightweight polling only where required
- short-lived authentication process
- cached protected-app configuration

---

# 9. Authentication Flow

```text
Protected application launched
            ↓
Is app protected?
            ↓
          YES
            ↓
Is active session valid?
       /           \
     YES            NO
      │              │
      ▼              ▼
 Allow app      Authentication UI
                     │
          ┌──────────┼──────────┐
          ▼          ▼          ▼
        Face     Windows Hello  PIN
          │
          ▼
       Liveness
          │
          ▼
     Face embedding
          │
          ▼
 Compare enrolled embeddings
          │
      ┌───┴────┐
     PASS     FAIL
      │          │
      ▼          ▼
   Unlock     Retry/fallback
```

---

# 10. Face Recognition Pipeline

## Enrollment

```text
Camera
  ↓
Detect face
  ↓
Check quality
  ↓
Check pose
  ↓
Capture multiple samples
  ↓
Alignment
  ↓
Embedding model
  ↓
Generate embeddings
  ↓
Validate consistency
  ↓
Encrypt embeddings
  ↓
Store locally
```

Do NOT store raw face photographs unless explicitly required.

Prefer storing only encrypted embeddings.

---

# 11. Authentication

During authentication:

```text
Camera frame
    ↓
Face detection
    ↓
Face quality check
    ↓
Liveness challenge
    ↓
Face alignment
    ↓
Embedding
    ↓
Similarity comparison
    ↓
Threshold decision
```

The threshold must be configurable during development but should not be exposed as an unrestricted "security slider" in the final consumer UI.

A low threshold can increase false acceptance risk.

A high threshold can increase false rejection.

The final threshold should be determined from measured validation data.

---

# 12. Liveness Detection

The original FaceGate uses head-pose challenges.

For Windows, implement:

```text
Challenge:
"Turn your head left"

        ↓

Camera frames

        ↓

Face landmarks / pose estimation

        ↓

Required movement detected?

   YES ─────→ Continue authentication
   NO  ─────→ Retry
```

Potential challenges:

- Turn left
- Turn right
- Look up
- Look down
- Blink
- Randomized sequence

Do not use only a single static image check.

Important:

A webcam-based system is still not equivalent to a hardware depth-sensing biometric system.

The application should explicitly describe itself as a local application-lock convenience/security layer, not a substitute for Windows account security or hardware-backed biometric security.

---

# 13. Windows Hello

Windows Hello should be treated as a fallback/alternative authentication mechanism.

Flow:

```text
Face recognition
       │
       ├── success → unlock
       │
       └── failure
              ↓
        Windows Hello
              ↓
        PIN / biometric
              ↓
           unlock
```

Do not attempt to bypass or reproduce Windows Hello's secure biometric system.

Use Microsoft's supported Windows authentication APIs.

---

# 14. Secure Storage

## Secrets

Never store:

```text
Plain password
Plain PIN
Raw encryption key
Plain face embedding
```

Use:

```text
Windows Credential Manager
DPAPI / protected storage
AES-256-GCM for application data
```

Suggested architecture:

```text
Face embedding
      ↓
Serialize
      ↓
AES-256-GCM
      ↓
Encrypted blob
      ↓
Local application storage

Encryption key
      ↓
Protected Windows credential/key storage
```

Use separate keys for different purposes where practical.

---

# 15. Password/PIN Security

If using a PIN/password fallback:

```text
Password
   ↓
Random salt
   ↓
Password KDF
   ↓
Verifier
```

Prefer a modern password KDF such as:

- Argon2id
- PBKDF2-HMAC-SHA-256 where platform constraints require it

Do not use plain SHA-256 as a password hashing scheme.

Add:

- rate limiting
- failed-attempt delay
- optional lockout
- secure comparison
- no password logging

---

# 16. Database

Keep the first version simple.

Use SQLite for:

```text
Protected applications
Settings
Schedules
Session metadata
Audit events
```

Do not store sensitive biometric material in ordinary SQLite plaintext.

Example:

```text
SQLite
 ├── ProtectedApps
 ├── Settings
 ├── Schedules
 └── AuditEvents

Secure storage
 └── Encryption keys / credential secrets

Encrypted files
 └── Face embeddings
```

---

# 17. Sleep / Lock Handling

Windows events should trigger:

```text
Windows Lock
Windows Unlock
Sleep
Resume
Session switch
User sign-out
User sign-in
```

When Windows locks or sleeps:

```text
Invalidate all active app sessions
       ↓
Protected applications require authentication again
```

---

# 18. Session Timer

Support:

### Lock immediately

```text
Unlock → use app → authentication expires immediately
```

### Fixed duration

```text
Unlock → 10 minutes → lock again
```

### Focus-based

```text
Unlock
  ↓
App loses focus
  ↓
Start timer
  ↓
Timer expires
  ↓
Require authentication
```

### Keep unlocked

Optional and should be clearly marked as lower-security convenience mode.

---

# 19. Scheduling

Example:

```text
Chrome
09:00–18:00 → protected
18:00–23:00 → normal
```

Also support:

```text
Monday
Tuesday
Wednesday
...
```

Do not use a busy loop for scheduling.

Use Windows scheduling/timers and wake only when needed.

---

# 20. System Tray

The application should run primarily in the system tray.

Tray menu:

```text
FaceGate
──────────────
Protected Apps
Authentication
Lock All
Pause Protection
Settings
Security Status
View Logs
About
Exit
```

Important:

If "Exit" is protected, require authentication before disabling the security agent.

---

# 21. UI Design

Use a modern Windows-native design.

Main dashboard:

```text
┌─────────────────────────────────────────┐
│ FaceGate                         ● ON   │
├─────────────────────────────────────────┤
│                                         │
│ Protected Applications                  │
│                                         │
│  Chrome                         🔒      │
│  VS Code                        🔒      │
│  WhatsApp                       🔒      │
│  Telegram                       🔒      │
│                                         │
│             + Add Application           │
│                                         │
├─────────────────────────────────────────┤
│ Authentication                          │
│                                         │
│ Face Recognition              Enabled   │
│ Windows Hello                 Available │
│ PIN                           Enabled   │
└─────────────────────────────────────────┘
```

Keep animations light because the laptop has 8 GB RAM.

---

# 22. Camera Resource Strategy

This is extremely important for the laptop.

Do NOT keep the webcam active continuously.

Instead:

```text
Idle
 ↓
Camera OFF

Protected app needs auth
 ↓
Camera ON

Authentication complete
 ↓
Camera OFF
```

This reduces:

- CPU usage
- RAM usage
- battery usage
- privacy exposure
- background resource consumption

Also release camera resources immediately after authentication.

---

# 23. AI Model Optimization for This Laptop

The laptop is not intended for model training.

Use inference only.

Recommended optimization:

```text
FP32 model
   ↓
FP16 / INT8 where validated
   ↓
Smaller ONNX model
   ↓
Lower input resolution
   ↓
CPU-efficient inference
```

Start with CPU inference.

Only add GPU acceleration after profiling.

Do not assume the Radeon hardware path will improve every model.

Benchmark:

```text
CPU inference
vs
DirectML
```

Use whichever is actually faster and more stable on the machine.

---

# 24. Recommended Model Strategy

Use lightweight models.

Avoid very large face models.

Target:

```text
Face detector:
small / fast

Embedding model:
MobileFaceNet-class or similarly lightweight model

Liveness:
small anti-spoofing model or landmark/pose-based approach
```

Model selection must consider:

- license
- accuracy
- CPU latency
- memory
- Windows compatibility
- commercial redistribution rights

Do not download random models from untrusted websites.

---

# 25. Performance Targets

Initial engineering targets:

| Metric | Target |
|---|---:|
| App startup | < 2–3 sec target |
| Idle CPU | Near 0% |
| Camera startup | < 1–2 sec target |
| Face inference | < 500 ms target |
| Authentication | < 3 sec target |
| Idle RAM | < 250 MB target |
| Authentication RAM | < 500 MB target |

These are targets, not guarantees.

Measure on the actual laptop.

---

# 26. Security Threat Model

Threats to consider:

### T1 — Photo spoofing

Mitigation:

- liveness challenge
- randomized challenge sequence
- face movement validation

### T2 — Video replay

Mitigation:

- randomized head movement
- temporal analysis
- liveness model

### T3 — Stolen configuration

Mitigation:

- encryption
- DPAPI
- Credential Manager

### T4 — Password guessing

Mitigation:

- rate limiting
- exponential delays
- secure KDF

### T5 — Process termination

Mitigation:

- background service
- watchdog
- restart behavior

### T6 — Configuration tampering

Mitigation:

- authenticated settings changes
- ACLs
- protected configuration

### T7 — Uninstall bypass

Do not pretend this can be made impossible for a local administrator.

Instead:

- require administrator permission
- provide authenticated uninstall flow
- document the security boundary

### T8 — Malware with administrator/kernel privileges

Out of scope for this application.

Document this clearly.

---

# 27. Important Security Boundary

This application is an **application locker**, not a replacement for:

- Windows login
- Windows Hello
- BitLocker
- antivirus
- EDR
- Windows security controls

A sufficiently privileged local administrator or malware can potentially interfere with application-level protection.

The README should clearly communicate this.

---

# 28. Privacy

Default policy:

```text
No cloud
No telemetry
No face uploads
No remote recognition
No raw face-photo storage
No analytics
```

Optional update checking should be separated from biometric functionality.

The authentication pipeline should work completely offline.

---

# 29. Logging

Do not log:

```text
face images
face embeddings
passwords
PINs
encryption keys
authentication secrets
```

Safe logs:

```text
Authentication started
Authentication succeeded
Authentication failed
Protected app detected
Session expired
Camera permission denied
Windows Hello unavailable
Configuration changed
```

Use structured logging with log rotation.

---

# 30. Permissions

The application may need permissions depending on the chosen Windows implementation.

At minimum:

- Camera permission
- User-level startup/background execution
- Process observation
- Administrator elevation only where genuinely required

Do not request administrator privileges for the entire application if they are unnecessary.

Prefer least privilege.

---

# 31. Build Environment for This Laptop

Install:

1. Git
2. Visual Studio Community
3. .NET SDK
4. Windows App SDK / WinUI 3 tooling
5. Windows 11 SDK
6. ONNX Runtime
7. OpenCV
8. CMake only if a native dependency requires it
9. Python only for model preprocessing/testing

### Visual Studio workload

Install only the workloads required for:

```text
.NET desktop development
Windows App SDK / WinUI
C++ desktop tools only if a dependency requires native compilation
```

Avoid installing unnecessary workloads because the laptop has limited disk/RAM resources.

---

# 32. Development Workflow

Recommended:

```text
Git
 ↓
Feature branch
 ↓
Implement small component
 ↓
Unit test
 ↓
Build
 ↓
Run locally
 ↓
Security review
 ↓
Commit
```

Do not ask an AI coding agent to rewrite the entire project in one operation.

Build subsystem-by-subsystem.

---

# 33. Development Phases

## Phase 0 — Repository study

Study FaceGate-Mac:

- folder structure
- authentication
- face pipeline
- app monitor
- locking
- timers
- settings
- security
- camera
- scheduling

Create a Windows mapping document before coding.

---

## Phase 1 — Windows shell

Build:

- WinUI 3 application
- system tray
- settings
- startup behavior
- configuration storage

No face recognition yet.

---

## Phase 2 — Application monitor

Implement:

- process detection
- protected-app list
- app identification
- basic lock state

Test with:

- Notepad
- Calculator
- Chrome
- VS Code

---

## Phase 3 — Authentication UI

Implement:

```text
Authentication window
PIN fallback
Windows Hello integration
Authentication result
```

Do not add biometric AI until this flow is stable.

---

## Phase 4 — Camera

Implement:

- camera discovery
- camera selection
- permission handling
- start/stop
- external webcam support
- error recovery

Test camera lifecycle aggressively.

---

## Phase 5 — Face detection

Add ONNX detector.

Pipeline:

```text
Camera
 ↓
Frame
 ↓
Resize
 ↓
Face detection
 ↓
Crop
```

Benchmark CPU usage.

---

## Phase 6 — Face embeddings

Add embedding model.

Enrollment:

```text
Capture
 ↓
Detect
 ↓
Align
 ↓
Embed
 ↓
Validate
 ↓
Encrypt
 ↓
Store
```

Authentication:

```text
Capture
 ↓
Embed
 ↓
Compare
 ↓
Decision
```

---

## Phase 7 — Liveness

Add:

- head pose
- randomized challenge
- anti-replay checks
- timeout

Test with:

- photograph
- phone video
- monitor playback
- real face
- different lighting

---

## Phase 8 — Secure storage

Implement:

- DPAPI
- Credential Manager
- AES-GCM
- key lifecycle
- secure deletion where appropriate

---

## Phase 9 — Advanced locking

Add:

- session timers
- lock on Windows lock
- lock on sleep/resume
- schedules
- lock-all
- pause protection
- secure exit

---

## Phase 10 — Security hardening

Perform:

- threat modeling
- permission review
- process tampering tests
- configuration tampering tests
- authentication brute-force tests
- replay/spoof tests
- crash recovery
- service restart tests

---

## Phase 11 — Performance optimization

Profile:

- CPU
- RAM
- camera startup
- model inference
- UI responsiveness
- background process usage

Optimize only after measurement.

---

## Phase 12 — Packaging

Create:

```text
MSIX
```

or another signed Windows installer.

Include:

- application
- required runtime
- models
- configuration migration
- uninstall
- versioning

Do not ship unsigned binaries as the final release.

---

# 34. Testing Matrix

## Functional

- [ ] Add app
- [ ] Remove app
- [ ] Enable/disable protection
- [ ] Face enrollment
- [ ] Face authentication
- [ ] PIN authentication
- [ ] Windows Hello
- [ ] Session timeout
- [ ] Lock immediately
- [ ] Keep unlocked
- [ ] Schedule
- [ ] Lock on Windows lock
- [ ] Lock on resume
- [ ] System tray
- [ ] Restart
- [ ] Uninstall

## Camera

- [ ] Built-in camera
- [ ] USB webcam
- [ ] Camera unavailable
- [ ] Camera permission denied
- [ ] Camera disconnected
- [ ] Camera already in use
- [ ] Camera permission race
- [ ] Close auth window during permission prompt

## AI

- [ ] Different lighting
- [ ] Glasses
- [ ] Different hairstyle
- [ ] Different distance
- [ ] Multiple faces
- [ ] Partial face
- [ ] No face
- [ ] Two faces
- [ ] Photo attack
- [ ] Video attack
- [ ] Monitor replay

## Security

- [ ] Wrong PIN
- [ ] Repeated wrong PIN
- [ ] Modified config
- [ ] Deleted config
- [ ] Killed process
- [ ] Restarted service
- [ ] Permission changes
- [ ] Uninstall attempt
- [ ] Credential storage inspection
- [ ] Log inspection for secrets

---

# 35. Performance Testing on This Laptop

Before optimization, collect:

```text
CPU %
RAM MB
GPU %
Inference time
Camera startup time
Authentication time
Battery impact
```

Use Windows Task Manager plus application-level performance counters.

Example benchmark:

```text
Test:
10 authentication attempts

Record:
- median inference time
- 95th percentile inference time
- average CPU
- peak RAM
- false rejection count
```

Do not optimize based on a single test.

---

# 36. AI Accuracy Evaluation

Do not select the face threshold arbitrarily.

Create a validation dataset with consented participants.

Measure:

```text
True Accept Rate
False Accept Rate
False Reject Rate
Equal Error Rate where appropriate
Liveness attack acceptance
```

Evaluate separately across:

- lighting
- camera quality
- distance
- pose
- glasses
- different users

The dataset must be handled securely and ethically.

---

# 37. Resource-Saving Development Setup

Because the laptop has 8 GB RAM:

### Recommended while coding

```text
Visual Studio
+ Browser with limited tabs
+ Git
```

Avoid simultaneously running:

```text
Visual Studio
+ Docker
+ WSL
+ Android Studio
+ local LLM
+ multiple browsers
```

If WSL is used for scripts, keep its memory usage controlled.

---

# 38. What NOT to Do

Do not:

- train a large face model locally
- store raw facial images by default
- send face data to an API
- use a cloud face-recognition service for authentication
- store passwords in JSON
- hard-code encryption keys
- use plain SHA-256 as password hashing
- keep camera running permanently
- run high-frequency process polling
- require administrator privileges unnecessarily
- claim "unbreakable security"
- claim hardware-level biometric security
- disable Windows security features
- bypass Windows Hello security
- implement kernel drivers in the first version

---

# 39. MVP

The first usable version should contain only:

```text
✓ Windows desktop UI
✓ System tray
✓ Add protected application
✓ Process monitoring
✓ App lock
✓ PIN authentication
✓ Camera access
✓ Face enrollment
✓ Face recognition
✓ Local encrypted embeddings
✓ Basic liveness
✓ Session timeout
✓ Lock on Windows lock
✓ Basic logging
```

Do NOT build everything at once.

---

# 40. Version 2

After MVP:

```text
✓ Windows Hello
✓ Multiple faces
✓ External camera picker
✓ Advanced liveness
✓ Scheduling
✓ Focus-based timers
✓ Lock-all
✓ Secure settings
✓ Better recovery
✓ Installer
✓ Automatic updates
```

---

# 41. Version 3

Advanced:

```text
✓ Stronger anti-spoofing
✓ Better model optimization
✓ Hardware acceleration where useful
✓ Advanced audit log
✓ Enterprise policy support
✓ Signed binaries
✓ Security audit
✓ Automated CI/CD
✓ Accessibility improvements
```

---

# 42. AI Coding Agent Instructions

If using an AI coding agent, give it these rules:

```text
1. Do not rewrite the entire project at once.
2. Read the existing architecture before modifying it.
3. Implement one subsystem at a time.
4. Never invent Windows APIs.
5. Prefer official Microsoft APIs/documentation.
6. Keep biometric data local.
7. Never log secrets.
8. Never hard-code encryption keys.
9. Use async APIs where appropriate.
10. Release camera resources after authentication.
11. Write unit tests for security-sensitive logic.
12. Never silently weaken authentication.
13. Do not add administrator privileges unless required.
14. Explain security-sensitive changes before implementing them.
15. Build and test after each major subsystem.
16. Optimize for an 8 GB RAM development machine.
17. Keep AI models lightweight.
18. Do not train models locally.
19. Preserve the project's license requirements when reusing code.
20. Treat FaceGate-Mac as a reference implementation, not as a specification that must be copied blindly.
```

---

# 43. Suggested First Milestone

The first milestone should be:

```text
Windows FaceGate MVP
```

with this exact flow:

```text
Start Windows
     ↓
FaceGate background agent
     ↓
User selects Chrome
     ↓
Chrome added to protected list
     ↓
User launches Chrome
     ↓
FaceGate detects process
     ↓
Authentication window
     ↓
PIN authentication
     ↓
Success
     ↓
Chrome allowed
     ↓
Session timer
     ↓
Session expires
     ↓
Chrome requires authentication again
```

Once this works reliably, replace/add the face-authentication path.

This isolates app-locking bugs from AI bugs.

---

# 44. Final Target Architecture

```text
                         FACEGATE WINDOWS
                                │
              ┌─────────────────┴─────────────────┐
              │                                   │
          WinUI 3 UI                         Background Agent
              │                                   │
              │                             App Monitor
              │                                   │
              └───────────────┬───────────────────┘
                              │
                       Authentication
                              │
            ┌─────────────────┼──────────────────┐
            │                 │                  │
          Face          Windows Hello           PIN
            │
        Liveness
            │
      Face Detection
            │
      Face Embedding
            │
      Secure Compare
            │
       Auth Decision
            │
            ▼
       App Lock Engine
            │
       ┌────┴────┐
       │         │
    Process   Session
    Control    Timer
       │         │
       └────┬────┘
            │
       Protected App

Security:
    DPAPI
    Credential Manager
    AES-256-GCM
    Secure KDF
    Least privilege
    Signed builds

AI:
    ONNX Runtime
    OpenCV
    Lightweight models
    CPU-first optimization
    Optional hardware acceleration
```

---

# 45. Definition of Done

The project is ready for a serious beta when:

- [ ] Protected apps reliably require authentication
- [ ] Face authentication works offline
- [ ] PIN fallback works
- [ ] Windows Hello works where supported
- [ ] Camera is not left running after authentication
- [ ] Face embeddings are encrypted
- [ ] Encryption keys are protected
- [ ] Password/PIN is not stored in plaintext
- [ ] Liveness rejects basic photo/video attacks
- [ ] Windows lock invalidates sessions
- [ ] Process monitor survives normal application crashes
- [ ] Configuration cannot be casually edited without authentication
- [ ] No secrets appear in logs
- [ ] CPU/RAM usage is acceptable on the Ryzen 5 4600H / 8 GB machine
- [ ] Automated tests pass
- [ ] Security tests pass
- [ ] Installer works
- [ ] Uninstaller works
- [ ] Documentation explains security limitations
- [ ] License/attribution requirements are satisfied
- [ ] Release binaries are signed before public distribution

---

# 46. First Commands

After installing Git and .NET:

```powershell
git clone https://github.com/dweep-desai/FaceGate-Mac.git
```

Use this repository for study/reference only.

Create a separate Windows project:

```powershell
mkdir FaceGate-Windows
cd FaceGate-Windows

dotnet new sln -n FaceGate
```

Then create the projects incrementally.

Do not start by copying Swift files into the Windows project.

---

# 47. Important Reference

Original project:

https://github.com/dweep-desai/FaceGate-Mac

The original README currently documents macOS 14+, on-device face recognition, multi-face enrollment, external cameras, liveness, Touch ID/password fallback, per-app timers, sleep locking, schedules, secure operations and encrypted local storage.

The repository currently has an active development history and a latest listed v1.2.1 release. Review the current upstream project before implementing a feature because its behavior may change.

Known upstream issues should also be reviewed during the port. For example, the repository has reported issues involving image orientation in the face embedding pipeline and camera/authentication lifecycle/thread-safety behavior. These should be treated as lessons for the Windows architecture rather than copied into the new implementation.

---

# 48. Bottom Line

This laptop is sufficient to **develop and run the Windows MVP**.

The main limitation is the **8 GB RAM**, so the project should use:

```text
C#
.NET
WinUI 3
ONNX Runtime
OpenCV
SQLite
DPAPI / Credential Manager
Windows Hello
Lightweight AI models
CPU-first inference
```

The development strategy should be:

```text
App Locker
   ↓
Authentication
   ↓
Camera
   ↓
Face Detection
   ↓
Face Embedding
   ↓
Liveness
   ↓
Encryption
   ↓
Security Hardening
   ↓
Performance Optimization
   ↓
Installer
```

Build the core application first, then add biometric AI.

That approach minimizes debugging complexity and is much more practical on an 8 GB Ryzen 5 4600H laptop.
