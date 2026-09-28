# Medistock — Offline-First Pharmacy ERP Platform

> **Modern, High-Performance, Multi-User Pharmacy Operating System for Windows**  
> Built with **.NET 9 / WinUI 3 (Windows App SDK)**, Clean Architecture, SQLite WAL (Dapper Hot Path / EF Core Cold Path), and Cloud Sync API.

---

## 🚀 Quick AI / Developer Auto-Setup Prompt

Copy and paste this prompt into your AI coding assistant (AGY CLI, Cursor, Copilot, Claude) to immediately setup, verify, and run the project:

```text
You are working on the Medistock Pharmacy ERP repository (D:\Projects\Medistock-offlinefirst).
Follow these exact steps to verify the environment, restore dependencies, run tests, and launch the app:

1. Environment Verification:
   Ensure .NET 9.0 SDK is installed (dotnet --version). Target platform is Windows x64.
2. Restore & Build:
   dotnet restore Medistock.sln
   dotnet build Medistock.sln -c Debug -p:Platform=x64
3. Automated Verification:
   Run all test suites:
   dotnet test tests/Medistock.Domain.Tests/Medistock.Domain.Tests.csproj
   dotnet test tests/Medistock.Infrastructure.Tests/Medistock.Infrastructure.Tests.csproj
   dotnet test tests/Medistock.Desktop.Tests/Medistock.Desktop.Tests.csproj
4. Build & Launch Desktop App:
   dotnet publish src/Clients/Medistock.Desktop/Medistock.Desktop.csproj -c Release -r win-x64 --self-contained true -o "dist/Medistock-Release-win-x64"
   Start-Process "dist/Medistock-Release-win-x64/Medistock.Desktop.exe"
5. Reference Directives:
   Always consult AGENT.md for architectural rules and AGY_STATE.md for progress history.
```

---

## 🛠️ Prerequisites & Requirements

- **Operating System:** Windows 10 (version 19041 or higher) / Windows 11 (64-bit)
- **SDK:** [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Workloads / Tools:**
  - Windows App SDK / WinUI 3
  - Inno Setup 6 (optional, for compiling standalone Windows installer)
- **IDE:** Visual Studio 2022 (v17.12+ recommended with *.NET Desktop Development* workload) or VS Code.

---

## 📂 Project Architecture & Structure

The repository follows **Clean Architecture** with a strict separation between hot-path transactional performance and cold-path administration:

```text
Medistock-offlinefirst/
├── src/
│   ├── Core/
│   │   ├── Medistock.Domain/              # Pure domain entities, aggregate roots, value objects, domain events
│   │   └── Medistock.Application/         # CQRS commands, queries, DTOs, service interfaces
│   ├── Infrastructure/
│   │   ├── Medistock.Infrastructure.Data/ # SQLite WAL data access (Dapper hot path + EF Core cold path)
│   │   ├── Medistock.Infrastructure.Hardware/ # Receipt printing, barcode HAL, thermal printer engine
│   │   ├── Medistock.Infrastructure.Identity/ # License activation, PasswordVault JWT storage, security
│   │   └── Medistock.Infrastructure.Sync/ # Update checker, AES-256 cloud backup, network monitor
│   ├── Shared/
│   │   └── Medistock.Contracts/           # Shared REST/IPC request/response DTOs
│   ├── Services/
│   │   ├── Medistock.CloudApi/            # ASP.NET Core Cloud Management & Activation Backend
│   │   └── Medistock.UpdaterService/      # Windows Service for background silent updates via Named Pipe
│   └── Clients/
│       └── Medistock.Desktop/             # WinUI 3 desktop client (MVVM, XAML, POS billing dashboard)
├── tests/
│   ├── Medistock.Domain.Tests/            # Domain logic and accounting rules unit tests
│   ├── Medistock.Infrastructure.Tests/    # SQLite hot path, licensing, backup, and sync tests
│   └── Medistock.Desktop.Tests/           # POS, keyboard navigation, and viewmodel tests
├── installer/
│   └── MedistockSetup.iss                 # Inno Setup compilation script
├── AGENT.md                               # AI agent protocol & execution directives
└── AGY_STATE.md                           # Project state log & milestone tracking
```

---

## 💻 How to Start Development

### 1. Clone the Repository
```powershell
git clone git@github.com:kawsarahmed15/offline-medistock-exe.git
cd offline-medistock-exe
```

### 2. Restore NuGet Dependencies
```powershell
dotnet restore Medistock.sln
```

### 3. Build the Solution
```powershell
dotnet build Medistock.sln -c Debug -p:Platform=x64
```

### 4. Run Automated Tests
```powershell
dotnet test
```
*(All 146 unit & integration tests should pass with 100% success).*

### 5. Run the Desktop App (Debug Mode)
```powershell
dotnet run --project "src/Clients/Medistock.Desktop/Medistock.Desktop.csproj" -p:Platform=x64
```

### 6. Publish Self-Contained Release
To produce a standalone zero-dependency distribution:
```powershell
dotnet publish "src/Clients/Medistock.Desktop/Medistock.Desktop.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o "dist/Medistock-Release-win-x64"
```
Launch the generated executable:
```powershell
Start-Process "dist/Medistock-Release-win-x64/Medistock.Desktop.exe"
```

---

## 🔑 First-Time Activation & Testing Credentials

When the application is launched for the first time, an activation modal will appear. It authenticates with the live production cloud backend.

- **Cloud Backend API:** `https://offline-medistock.teklin.in`
- **Test Credentials:**
  - **Email:** `zafar@teklin.in`
  - **Password:** `password`

Upon activation, a cryptographically signed license JWT is stored securely in the Windows `PasswordVault` and `%LOCALAPPDATA%\Medistock\Config\activation.json`.

---

## 📦 Creating the Installer (Inno Setup)

To build the setup installer executable:
1. Ensure [Inno Setup 6](https://jrsoftware.org/isdl.php) is installed.
2. Publish the desktop app to `dist/Medistock-Release-win-x64`.
3. Compile the installer:
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/MedistockSetup.iss
```
The installer will be generated at `installer/Output/MedistockSetup-1.0.0.exe`.

---

## ⚙️ Local Data Storage Architecture

All local data is isolated per-user in `%LOCALAPPDATA%\Medistock\`:
- **Database:** `%LOCALAPPDATA%\Medistock\Data\medistock.db` (SQLite WAL mode + FTS5 search index)
- **Local Backups:** `%USERPROFILE%\Documents\Medistock\Backups\`
- **Configuration:** `%LOCALAPPDATA%\Medistock\Config\`
- **Logs:** `%LOCALAPPDATA%\Medistock\Logs\`
- **Updates:** `%LOCALAPPDATA%\Medistock\Updates\`

---

## 📜 Coding Directives & Guidelines

- Refer to [AGENT.md](file:///D:/Projects/Medistock-offlinefirst/AGENT.md) for mandatory agent directives (understand & clarify requirements before changes, mandatory auto-build and launch after every modification).
- Refer to [AGY_STATE.md](file:///D:/Projects/Medistock-offlinefirst/AGY_STATE.md) for current architectural decisions and milestone logs.
