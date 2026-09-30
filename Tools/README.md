# Repository Tools

This folder is reserved for repository-level automation that operates across both Unity projects. Unity Editor-only extraction, validation, and build code belongs in `Packages/com.thequantifier.isekai.project-tools` or the owning project's `Assets/Editor` folder; generated executables and caches do not belong here.

`UIG.ps1` provides the terminal launcher installed by dot-sourcing it from the user's PowerShell profile. Run `uig help` for the complete command list. Common commands include:

- `uig server help` or `uig client help` to show only the commands for that side
- `uig server start`, `stop`, `restart`, `status`, or `logs`
- `uig client start`, `end`, `restart`, `status`, or `logs` for the default client
- `uig client <username-or-userID> start` to prefill an account identifier (a password is still required)
- `uig clients status` or `uig clients end` to inspect or stop clients started through the launcher
- `uig build <client|server|all>` to run the Unity command-line builds
- `uig doctor` and `uig paths` to diagnose the local setup

`uig server start` returns only after the server log confirms that the authoritative world is loaded and the transport is listening. The launcher creates a shared local authentication token in the ignored `.uig` directory and passes it to both sides; do not copy that token into source control.

`uig client start` first establishes an app-authenticated connection, then opens the in-game login screen. Enter a username and password and choose **Create Account** on first use; the server assigns an opaque random 256-bit user ID. Later, Login accepts either the username or that user ID plus the password. The optional launcher identifier only prefills the form—it never bypasses password verification. Account verification and account files are owned only by the dedicated server; the launcher never accepts or stores account passwords.
