# FASX Flows — local first release

## Scope

The first Flows release runs only inside the Windows companion on the Windows user's desktop. It does not accept remote flow-run requests from the Android phone, a network client, or the internet. A future remote-trigger feature would need its own session permission and per-flow approval.

## Registered actions

A flow can contain up to eight steps. Only the following action types are accepted:

1. **Open built-in app:** Notepad, Calculator, Paint, or File Explorer. Each friendly ID maps to a fixed Windows executable name in the application.
2. **Open HTTPS website:** opens a user-entered HTTPS URL in the system browser. URLs containing embedded credentials or a non-standard port are rejected; localhost is rejected.
3. **Choose and open folder:** opens a native folder picker at run time, then opens only the selected folder. The folder path is not saved into the flow.

There is no script action, shell action, arbitrary executable path, arbitrary command arguments, key injection, file deletion, or power-control action.

## Local storage and limits

- Flow definitions are stored in the current Windows user's Local AppData under `FASX/flows-v1.json`.
- The store holds at most 20 flows and is limited to 64 KB.
- A flow name is at most 40 characters.
- Each flow contains between one and eight steps.
- A website URL is at most 512 characters.
- Each flow is validated when created, loaded and run.

## Running a flow

The Windows user selects a saved flow and presses **Run selected flow**. FASX shows a step-by-step preview and requires an explicit Yes confirmation. Folder steps open a folder picker again for each run. If a step fails or the user cancels the folder picker, execution stops.

Flows are local conveniences, not unattended automation. They are not exposed through the remote session protocol.

## Future work

- Editing existing saved flows and user-defined safe cards.
- Import/export with strict schema validation.
- Optional additional registered actions after a permission and safety review.
- Remote flow triggering only with a separate, narrow permission and local Windows approval.
