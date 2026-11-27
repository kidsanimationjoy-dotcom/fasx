# Competitor Research → FASX Product Decisions

This document records product lessons, not copied designs or proprietary implementations.

| Product/reference | Publicly documented capability | FASX learning |
|---|---|---|
| RustDesk | Remote desktop, self-hosting, cross-platform clients | Evaluate its architecture and license before reuse |
| AnyDesk | Session permissions, unattended access modes, privacy tools | Make permissions understandable and granular |
| TeamViewer | Device management, multi-monitor and support workflows | Treat device and session administration as first-class |
| Chrome Remote Desktop | PIN-based remote access and temporary support codes | Keep onboarding simple |
| Splashtop | File transfer, audio, multi-monitor and remote support | Plan for quality profiles and media controls |
| Parsec | Low-latency streaming controls | Measure responsiveness instead of relying on marketing claims |
| MeshCentral | Device management, event history, diagnostics | Add a meaningful computer-health dashboard |
| DWService | Browser-based remote support and device tools | Add computer management beyond screen viewing |
| Zoho Assist | File manager and remote reboot workflows | Consider file operations outside an active desktop session |
| KDE Connect / Unified Remote | Touchpad, presentation and custom remote controls | Build SmartDeck and application-aware control panels |

## Product differentiation

FASX's strongest original direction is the combination of:
- SmartDeck: user-built command centre.
- Context Control: optional app-aware controls.
- FASX Flows: typed, permission-scoped workflows.
- Deck Gallery: reviewed, inert, shareable templates.
- Connection Guardian: transparent quality and recovery.
- Security Shield: visible consent and capability-based access.

The combination must be coherent and genuinely useful; a large feature checklist is not by itself differentiation.

## Source references

- RustDesk: https://github.com/rustdesk/rustdesk
- AnyDesk: https://anydesk.com/
- TeamViewer: https://www.teamviewer.com/
- Chrome Remote Desktop: https://remotedesktop.google.com/
- Splashtop: https://www.splashtop.com/
- Parsec: https://parsec.app/
- MeshCentral: https://meshcentral.com/
- DWService: https://www.dwservice.net/
- Zoho Assist: https://www.zoho.com/assist/
- KDE Connect: https://kdeconnect.kde.org/
- Unified Remote: https://www.unifiedremote.com/

Feature availability varies by operating system, subscription, and configuration. Verify current vendor documentation during implementation.
