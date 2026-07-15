<div align="center">
  <img src="assets/ping-tester.png" width="128" alt="Ping Tester icon">
  <h1>Ping Tester</h1>
  <p>A small Windows desktop tool for recording latency, packet loss, and connection outages without living in a terminal.</p>
  <p>
    <a href="https://ko-fi.com/K3Q5236GOO"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support me on Ko-fi"></a>
  </p>
  <p>
    <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-58adff" alt="MIT License"></a>
    <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-4fe0c1" alt="Windows 10 and 11">
  </p>
</div>

After dealing with several connection problems at home—badly crimped cables, misconfigured repeaters, and plenty of other DIY mishaps—I decided I needed a tool to test the connection, detect problems, and prove to myself that the issues were real rather than imagined.

## What it does

- Runs the bundled PowerShell ping test without showing a console window.
- Configures duration, interval, and one or more IP addresses or hostnames from the UI.
- Displays latency, packet loss, outages, charts, and individual samples in real time.
- Stores CSV and JSON results for later comparison.
- Lists completed, manually stopped, and interrupted runs in a local history.
- Opens external CSV or JSON result files without importing or modifying them.
- Supports English and Spanish, with English used on first launch.
- Ships as a single portable `PingTester.exe` file.

## Requirements

### To run

- Windows 10 or Windows 11, x64.
- Windows PowerShell 5.1.
- Microsoft Edge WebView2 Runtime.

Both PowerShell 5.1 and WebView2 are normally already available on supported Windows installations.

### To build

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.
- Windows x64.

## Configuration

Use the **New test** panel in the application:

| Setting | Description | Default |
| --- | --- | --- |
| Duration | Test duration in whole minutes. | `30` |
| Interval | Delay between pings in seconds; decimals are supported. | `1` |
| Targets | IP addresses or hostnames, one per line. | `1.1.1.1`, `8.8.8.8` |

The application remembers the last configuration and selected language. Generated results are stored in:

```text
%LOCALAPPDATA%\PingTester\results
```

Application preferences and run metadata stay under `%LOCALAPPDATA%\PingTester`. The CSV and JSON result files remain usable independently of the application.

## Build

Restore and compile the project from the repository root:

```powershell
dotnet restore .\src\PingTester.csproj
dotnet build .\src\PingTester.csproj -c Release
```

Publish the portable Windows x64 executable:

```powershell
dotnet publish .\src\PingTester.csproj `
  -c Release `
  -r win-x64 `
  -o .\dist
```

The distributable output is:

```text
dist\PingTester.exe
```

The publish configuration is self-contained and single-file. Users do not need the .NET runtime, source files, PowerShell script, HTML interface, or additional DLLs beside the executable.

## Publishing a release

1. Move the entries under **Unreleased** in [`CHANGELOG.md`](CHANGELOG.md) to a versioned section with its release date.
2. Replace the contents of [`RELEASE-NOTES.md`](RELEASE-NOTES.md) with the public notes for the new version.
3. Commit and push both files and all release changes.
4. Open **Actions → Publicar release → Run workflow**, enter a SemVer version without the `v` prefix, and run it.

The workflow validates the version, builds the self-contained Windows x64 executable with that version in its metadata, creates the `v<version>` tag and GitHub release, copies `RELEASE-NOTES.md` verbatim into the release description, and uploads `PingTester-v<version>-win-x64.exe`. GitHub rejects the publication if a release with the same tag already exists.

## Project structure

```text
.
├── .github/workflows/    # Manual GitHub release workflow
├── .old/                 # Original standalone PowerShell script and HTML viewer
├── assets/               # README assets
├── src/                  # C# host, embedded web UI, and application icon
│   ├── Resources/
│   │   └── ping_test.ps1 # Maintained PowerShell runner embedded in the app
│   └── PingTester.csproj
├── dist/                 # Generated portable executable
├── CHANGELOG.md          # Permanent release history
├── RELEASE-NOTES.md      # Notes copied into the next GitHub release
├── LICENSE
└── README.md
```

The files under `.old/` are the untouched legacy PowerShell script and HTML viewer. They are preserved for reference and are never used as build inputs. The maintained script is `src/Resources/ping_test.ps1`, which is embedded in the application.

## About Me

**Alex Adiaconitei**

Software engineer specialized in backend development with Java and Spring Boot, comfortable working with JavaScript and Python. I enjoy building personal projects to keep learning, with a growing interest in infrastructure.

- Backend Software Engineer
- Java, Spring Boot, JavaScript, Python
- Remote

[GitHub](https://github.com/AlexAdiaconitei) · [LinkedIn](https://www.linkedin.com/in/alexadiaconitei/) · [Ko-fi](https://ko-fi.com/K3Q5236GOO)
