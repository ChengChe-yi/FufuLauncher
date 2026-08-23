# Security Policy

## Supported Versions

FufuLauncher ships with a built-in updater. Only the most recent release
receives security fixes. Before reporting a vulnerability, please update to
the latest version and confirm the issue can still be reproduced.

| Version | Supported          |
| ------- | ------------------ |
| latest  | :white_check_mark: |
| older   | :x:                |

## Reporting a Vulnerability

**Do NOT open a public GitHub issue for security problems.**

Please report vulnerabilities privately to the maintainer:

- **Telegram:** [https://t.me/codecubist](https://t.me/codecubist)

If private vulnerability reporting is enabled on this repository, you may
also use **Security → Report a vulnerability** on GitHub.

### What to Include

- The FufuLauncher version you are running
- Your Windows version and build number (e.g. Windows 11 23H2, Build 22631)
- Detailed steps to reproduce the issue, or a proof of concept
- The potential impact (e.g. credential disclosure, arbitrary code
  execution, privilege escalation)

### What to Expect

- Reports are reviewed on a **best-effort** basis. Whether a report is
  accepted, declined, or deferred — and whether a fix is released — is
  decided case by case, based on the severity and scope of the impact.
  No guarantee of action or remediation is made.
- There is no guaranteed response time.
- Coordinated disclosure is appreciated: please refrain from making a
  report public until it has been reviewed.
- Credit in the release notes is possible but not guaranteed, and only if
  you wish to be credited.

## Scope

In scope:

- The FufuLauncher desktop application in this repository
- The built-in update mechanism
- Local storage and handling of account credentials and other sensitive data

Out of scope:

- Vulnerabilities in the game client itself — report them to the game vendor
- Vulnerabilities in third-party dependencies — report them upstream, but
  feel free to notify us as well
- Issues in the injection module — report them to
  [FufuLauncher.UnlockerIsland](https://github.com/FufuLauncher/FufuLauncher.UnlockerIsland)

## Verifying Official Releases

Official builds are code-signed courtesy of
[SignPath](https://signpath.org). Always download FufuLauncher from the
[Releases](https://github.com/FufuLauncher/FufuLauncher/releases) page or the
official website, and treat unsigned or unexpectedly modified binaries as
suspect.
