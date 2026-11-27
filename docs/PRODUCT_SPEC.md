# FASX Product Specification

## Product definition

FASX is an all-in-one personal computer command centre. It unites remote desktop, computer management, personalized controls, and user-approved automation for individuals and organizations.

## Audiences

- Students: presentation remote, study workflows, and file exchange.
- Everyday users: easy remote access, touchpad, keyboard, and computer health.
- Professionals: custom SmartDecks, application-aware controls, and approved workflows.
- Creators: media controls, multi-display workflows, and large-file transfer.
- Businesses and IT support: managed devices, scoped roles, audit history, and approved support sessions.

## Experience principles

1. One product, adaptable experiences. Users select a profile or build a custom SmartDeck.
2. Start simple. Essential actions are visible; advanced controls remain discoverable.
3. Show state clearly. Device online state, active session, permissions, connection quality, and transfer progress are never hidden.
4. Customize safely. Deck templates and Flows can call only registered FASX actions.
5. Ask before impact. Pairing, control input, file changes, power actions, and other sensitive operations require clear approval.

## Signature systems

### SmartDeck
A customizable home screen made from movable cards and action panels. Deck cards have a stable type, required permissions, configuration schema, and version.

### Context Control
Offers the appropriate panel for the foreground application, if the user enables application detection. It must never read application content or capture additional data merely to select a control panel.

### FASX Flows
A visual workflow builder that chains approved, typed actions. The first release should support simple sequences, explicit confirmation gates, cancellation, and execution logs. No arbitrary script, shell, or downloaded-code actions.

### Deck Gallery
A library for sharing signed/versioned SmartDeck layouts and safe Flow templates. Imported templates are inert until reviewed and enabled. Templates cannot contain executable binaries or unrestricted commands.

### FASX Intelligence
An explainable helper for interpreting locally collected device-health metrics. Recommendations are advisory by default; system modifications require a separate, explicit confirmation.

### File Bridge
A two-way file manager with scoped locations, transfer progress, checksums, cancellation, and conflict handling.

### Connection Guardian
Monitors connectivity, latency, packet loss where available, stream quality, and reconnect state. It communicates when a session is relayed rather than direct.

### Security Shield
Owns pairing, device identity, session approval, least-privilege permissions, visible activity, and device/session revocation.

### FASX Business
A later managed mode with organization-owned device groups, role-based administration, policy controls, and audit export. It must not weaken local user visibility or consent.

## First release scope

The first end-to-end release should include:
- Android-to-Windows device pairing with local confirmation.
- Direct LAN connection and internet relay fallback.
- Live screen viewing through the Windows system capture picker.
- Touchpad, click, scrolling, and remote keyboard with separate permissions.
- Visible active-session status and local disconnect.
- Connection quality and reconnection state.
- A basic SmartDeck with customizable cards.

File Bridge, diagnostics, Flows, and business administration follow as separate tested milestones.

## Explicit non-goals

- Hidden or covert access.
- Bypassing Windows security prompts or user consent.
- Arbitrary remote shell/PowerShell execution.
- Collecting passwords, browser cookies, or unrelated personal data.
- Installing persistence that conceals the app or its active state.
- Capturing the desktop without a clear, user-started capture flow.
