# LockGate for Windows

LockGate is a Windows-native application locker designed to monitor and secure specific applications on your computer. Built with C# and .NET 8 (WPF), it acts as a user-mode locker by utilizing PIN verification, session timers, and a background watcher to protect designated apps.

## :rocket: Features
- **Application Locking**: Monitor specific Windows processes and require authentication to use them.
- **PIN Authentication**: Secure PIN verifier with PBKDF2 hashing and throttling to prevent brute-force attacks.
- **Session Timers**: Configurable locking engine to handle authenticated sessions.
- **Background Protection**: Runs quietly in the system tray while monitoring protected apps.
- **JSON Configuration**: Manage protected applications and settings easily.

## :hammer_and_wrench: Architecture
The solution is divided into modular components:
- LockGate.Core: Platform-neutral core logic (session timers, lock engine, PIN verification, config).
- LockGate.Infrastructure: Infrastructure logic for interacting with Windows services and process monitoring.
- LockGate.App: The WPF-based Windows application, including the authentication window and system tray integration.

## :computer: Getting Started

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
- Windows 10/11.

### Build & Run
To build the solution and run the application:

`powershell
# Restore dependencies and build the solution
dotnet build

# Run the WPF application
dotnet run --project src/LockGate.App
`

### Running Tests
The project includes a suite of tests to verify the core logic:
`powershell
dotnet test
`

## :warning: Security Boundary
LockGate is an application locker and **not** a replacement for Windows sign-in, BitLocker, or Windows Hello. A local administrator or malware can bypass user-mode lockers. The PIN verifier provides basic application-level access control.

## :scroll: License
This project is open-source. Please add your own LICENSE file before distributing.
