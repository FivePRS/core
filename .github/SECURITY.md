# Security Policy

## Supported Versions

Only the latest release on the `main` branch receives security fixes. Older tags are not maintained.

| Version | Supported |
|---|---|
| Latest (`main`) | Yes |
| Older tags | No |

---

## Reporting a Vulnerability

**Do not open a public GitHub issue for security vulnerabilities.**

Report discovered vulnerabilities privately by emailing:

```
fiveprsteam@gmail.com
```

Include as much of the following as possible:

- A clear description of the vulnerability and its potential impact.
- The affected component or file(s).
- Steps to reproduce or a proof-of-concept (in a responsible disclosure context).
- Any suggested mitigations or fixes you have already identified.

---

## Response Timeline

| Stage | Target timeframe |
|---|---|
| Acknowledgement | Within 48 hours of receipt |
| Initial assessment | Within 5 business days |
| Fix or mitigation | Dependent on severity; critical issues within 14 days |
| Public disclosure | After a fix is released and downstream servers have had reasonable time to update |

We will keep you informed at each stage and credit you in the release notes unless you request otherwise.

---

## Scope

The following are within scope for security reports:

- Server-side remote code execution or privilege escalation via FivePRS event handlers.
- SQL injection in the database layer (`FivePRS.Server/Database/`).
- Client-side data that is trusted without server-side validation, enabling exploitation of other players.
- Exposure of server configuration secrets through client-accessible APIs.

The following are out of scope:

- Vulnerabilities in FiveM itself, CitizenFX, or the GTA V engine.
- Vulnerabilities in third-party dependencies (report those upstream).
- Issues that require physical or console access to the host machine.
- Denial of service attacks that require an already-banned or authenticated administrator account.

---

## Preferred Languages

We accept reports in English.
