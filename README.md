<div align="center">
  <img src="src/LockGate.App/Assets/logo_symbol.png" alt="LockGate Logo" width="150" />
  
  # LockGate for Windows
  
  **A Windows-native application locker designed to monitor and secure specific applications on your computer.**
  
  [![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat&logo=windows)](https://microsoft.com/windows)
  [![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet)](https://dotnet.microsoft.com/)
</div>

<br />

## 📖 Overview
Built with C# and .NET 8 (WPF), LockGate acts as a user-mode locker by utilizing PIN verification, session timers, and a background watcher to protect designated apps. It runs quietly in the system tray, stepping in only when a protected application is launched.

---

## ✨ Key Features
* 🔒 **Application Locking**: Actively monitor specific Windows processes and require authentication before use.
* 🔑 **PIN Authentication**: Secure PIN verifier backed by PBKDF2 hashing and throttling to mitigate brute-force attacks.
* ⏱️ **Session Timers**: Configurable locking engine to handle authenticated sessions without repeatedly asking for a PIN.
* 🛡️ **Background Protection**: Seamlessly runs in the system tray with minimal resource footprint.
* ⚙️ **JSON Configuration**: Simple and manageable JSON-based settings to easily add or remove protected applications.

---

## 🏗️ Architecture
The solution is divided into clean, modular components:
* LockGate.Core: Platform-neutral core logic (session timers, lock engine, PIN verification, config).
* LockGate.Infrastructure: Infrastructure logic for interacting with Windows services and process monitoring.
* LockGate.App: The WPF-based Windows application, including the authentication window and system tray integration.

---

## 🚀 Getting Started

### Prerequisites
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
* Windows 10/11 (target OS version 10.0.22621.0+).

### Build & Run
To build the solution and run the application locally:

`powershell
# 1. Restore dependencies and build the solution
dotnet build

# 2. Run the WPF application
dotnet run --project src/LockGate.App
`

### Running Tests
The project includes a suite of unit tests to verify the core business logic. Run them using:
`powershell
dotnet test tests/LockGate.Core.Tests
`

---

## ⚠️ Security Boundary
LockGate is an application locker and **not** a replacement for Windows sign-in, BitLocker, or Windows Hello. 
* A local administrator or sophisticated malware can bypass user-mode lockers.
* The PIN verifier provides basic application-level access control but is only as private as its storage medium.

---

## 📜 License
This project is open-source. Please add your own LICENSE file before distributing.

---

## 💡 Acknowledgements & Inspiration
* Inspired by [**FaceGate-Mac**](https://github.com/dweep-desai/FaceGate-Mac) by [dweep-desai](https://github.com/dweep-desai) — big credit for the concept and bringing the seamless app-locking experience to Windows!

---

## ☕ Support
If you enjoy using LockGate and want to support continued development, you can buy us a coffee:

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-Support%20Us-yellow?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black)](https://buymeacoffee.com/flerken)

