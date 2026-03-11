# Contributing to FivePRS

Thank you for taking the time to contribute. This document covers everything you need to know before submitting an issue or a pull request.

---

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Setup](#development-setup)
4. [Project Structure](#project-structure)
5. [Coding Standards](#coding-standards)
6. [Submitting Changes](#submitting-changes)
7. [Reporting Bugs](#reporting-bugs)
8. [Feature Requests](#feature-requests)

---

## Code of Conduct

All contributors are expected to engage respectfully. Harassment, discrimination, or abusive behaviour of any kind will result in immediate removal from the project.

---

## Getting Started

1. Fork the repository and clone your fork locally.
2. Create a dedicated branch for your work:

   ```
   git checkout -b feature/your-feature-name
   ```

3. Make your changes, commit them with a clear message, and open a pull request against `main`.

---

## Development Setup

**Prerequisites**

- .NET 6 SDK
- Visual Studio 2022 or JetBrains Rider (recommended)
- A local FiveM server build (for live testing)

**Building**

Open `FivePRS.sln` and build with Visual Studio, or run:

```
dotnet build FivePRS.sln -c Release
```

Compiled outputs land in `bin/client/` and `bin/server/` as configured in each project file.

**Testing locally**

Copy the relevant DLLs from `bin/` to your FiveM server's `resources/FivePRS/` directory and restart the resource. There is no automated test suite yet; manual in-server testing is the current validation path.

---

## Project Structure

| Project | Purpose |
|---|---|
| `FivePRS.Core` | Shared models, events, interfaces, and config POCOs. No FiveM API dependency. |
| `FivePRS.Client` | Client-side scripting: agency management, callout dispatch, vehicle spawning, loadouts. |
| `FivePRS.Server` | Server-side scripting: player data, XP, database abstraction. |
| `FivePRS.Police` | Police department implementation: callouts, vehicles, loadouts. |

Callout packs and plugin extensions are built as separate class libraries and dropped into the `callouts/` or `plugins/` folders at runtime. They reference `FivePRS.Core` and `FivePRS.Client` but are never compiled into the core solution.

---

## Coding Standards

- **Language:** C# 10, targeting `net6.0`.
- **Nullability:** Nullable reference types are enabled across all projects. Do not suppress warnings without a comment explaining why.
- **Naming:** Follow standard C# conventions. Public members use PascalCase; private fields use `_camelCase`.
- **Namespaces:** Match the folder structure. New files in `FivePRS.Client/Callouts/` belong to `namespace FivePRS.Client.Callouts`.
- **Comments:** Write XML doc comments on all public types and members. Inline comments should explain intent, not restate what the code does.
- **Error handling:** Catch only what you can meaningfully recover from. Log with `Debug.WriteLine("[FivePRS] ...")` and include context.
- **Async:** Prefer `async Task` over `async void` except where CitizenFX event handlers specifically require `void`.
- **Formatting:** Use the `.editorconfig` or the project defaults. No tabs; four-space indentation.

---

## Submitting Changes

- Keep pull requests focused. One logical change per PR.
- Fill in the pull request template completely.
- Reference the related issue number if one exists (e.g. `Closes #42`).
- All CI checks must pass before a PR will be reviewed.
- Expect a review within a few business days. Please do not ping maintainers repeatedly.

---

## Reporting Bugs

Open a GitHub Issue and include:

- A concise description of the problem.
- Steps to reproduce.
- Expected and actual behaviour.
- Server OS, FiveM build number, and any relevant `server.log` output.

Do not paste large log files inline. Attach them or use a paste service.

---

## Feature Requests

Open a GitHub Issue with the `enhancement` label. Describe the use case, not just the implementation idea. Features that serve a broad range of server configurations are prioritised over niche scenarios.

---

## License

By contributing you agree that your work will be licensed under the same license as this project. See [LICENSE](LICENSE) for details.
