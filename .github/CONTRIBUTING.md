# Contributing to FufuLauncher

Thank you for your interest in contributing to FufuLauncher! We welcome all kinds of contributions, including bug reports, feature requests, documentation improvements, translations, and code changes. Please take a moment to read these guidelines before you start.

## Code of Conduct

This project and everyone participating in it is governed by the [Contributor Covenant Code of Conduct](../CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to the project maintainers.

## Ground Rules

- Be respectful and considerate when communicating with other contributors and maintainers.
- Follow the [Code of Conduct](../CODE_OF_CONDUCT.md).
- Create an issue for any major change or new feature before submitting a pull request. Discuss the change transparently and get community feedback first.
- Keep changes focused and as small as possible. One logical change per pull request.
- Do not submit generated files, build artifacts, or third-party binaries.
- Respect the project's license (MIT). By contributing, you agree that your contributions will be licensed under the same license.

## What We Are Looking For

We welcome contributions in many forms:

- Bug reports and feature requests via [GitHub Issues](https://github.com/FufuLauncher/FufuLauncher/issues).
- Documentation improvements.
- Translations of the user interface and documentation.
- Code fixes and new features.
- Help triaging and reproducing issues.

## What We Are Not Looking For

- Do not use the issue tracker for general support questions. Please check the README and documentation first.
- Do not submit pull requests that are unrelated to the project's purpose.
- Do not submit changes that bypass the game's terms of service or facilitate cheating.

## Getting Started

### Prerequisites

- Windows 10 or Windows 11.
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later.
- The [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/).
- Visual Studio 2022 (or later) with the workloads for Windows application development, WinUI, and Windows App SDK.

### Building the Project

1. Clone the repository:
   ```bash
   git clone https://github.com/FufuLauncher/FufuLauncher.git
   ```
2. Open `FufuLauncher.sln` in Visual Studio, or build from the command line:
   ```bash
   dotnet build FufuLauncher\FufuLauncher.csproj -c Release
   ```

## How to Report a Bug

If you find a security vulnerability, do **NOT** open a public issue. Please see [SECURITY.md](../SECURITY.md) for instructions on how to report it privately.

For all other bugs, open an issue using the **Bug Report** template. Please include:

- A clear description of the problem.
- Detailed steps to reproduce the issue.
- The FufuLauncher version, game server, and operating system version.
- Relevant logs and screenshots, if available.

## How to Suggest a Feature or Enhancement

Open an issue using the **Suggestions & Feature Requests** template. Please describe:

- The problem or limitation you are facing.
- Your proposed solution.
- The expected benefit of the change.
- Any alternatives you have considered.

## Submitting a Pull Request

1. Fork the repository and create a new branch for your change.
2. Make your changes, following the existing code style.
3. Build and test your changes locally.
4. Write clear, descriptive commit messages.
5. Submit a pull request and describe what you changed and why.
6. Reference any related issues in the pull request description.

## Code Review Process

- Maintainers review pull requests as time permits.
- A pull request may be closed if it does not receive a response to review feedback within two weeks.
- The maintainers may request changes before merging. Please keep the branch up to date with the latest `master` branch.

## Style Guide

- Follow the existing code style and the conventions in [.editorconfig](../.editorconfig).
- Write commit messages in a clear, descriptive style (for example, `fix: ...`, `feat: ...`, `docs: ...`).
- Use the [Conventional Commits](https://www.conventionalcommits.org/) format for commit messages where practical.

## Community

- Discussions and issue tracking happen on [GitHub](https://github.com/FufuLauncher/FufuLauncher).
- Project website: [fu1.fun](https://fu1.fun/)
- Injection module repository: [FufuLauncher.UnlockerIsland](https://github.com/FufuLauncher/FufuLauncher.UnlockerIsland)
