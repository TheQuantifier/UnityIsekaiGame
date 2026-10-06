# Repository Tools

This folder is reserved for repository-level automation that operates across both Unity projects. Unity Editor-only extraction, validation, and build code belongs in `Packages/com.thequantifier.isekai.project-tools` or the owning project's `Assets/Editor` folder; generated executables and caches do not belong here.

`UIG.ps1` provides the terminal launcher installed by dot-sourcing it from the user's PowerShell profile. Run `uig help` for the complete command list. Common commands include:

- `uig server help` or `uig client help` to show only the commands for that side
- `uig server start`, `stop`, `restart`, `status`, `logs`, or `captures`
- `uig client start`, `end`, `restart`, `status`, or `logs` for the default client
- `uig clients status` or `uig clients end` to inspect or stop clients started through the launcher
- `uig game start`, `end`, `restart`, `status`, or `logs` to control the server and client together
- `uig trace start` to arm runtime logging and open the live graph before or after starting the game
- `uig trace end` to stop tracing while leaving the server and client running
- `uig trace status`, `summary`, `sessions`, `logs [lines]`, or `graph <start|end|status>` to inspect and control tracing
- `uig build <client|server|all>` to run the Unity command-line builds
- `uig doctor` and `uig paths` to diagnose the local setup

`uig server start` returns only after the server log confirms that the authoritative world is loaded and the transport is listening. The launcher creates a shared local authentication token in the ignored `.uig` directory and passes it to both sides; do not copy that token into source control.

The dedicated server shuts itself down cleanly after two continuous minutes with no remote client connected. A client waiting at the login screen counts as connected and cancels the countdown; after the last client disconnects, a fresh two-minute countdown begins. Shutdown saves authoritative player and world state before the server process exits.

Start the two processes individually with `uig server start` followed by `uig client start`, or start them together with `uig game start`. `uig trace start` can run before or after those commands and automatically connects when both processes appear. `uig trace end` detaches tracing without stopping either process.

`uig server captures [count]` summarizes the most recent instrumented world-checkpoint captures, prints the ten slowest participant frames, and reports the latest background write. Each autosave records every capture frame as `frame|participant|elapsedMs|allocatedBytes`; the launcher archives these records in `Logs/Runtime/server-captures.log` before rotating `server.log`, so later analysis retains raw timing/allocation measurements, distribution statistics, payload reuse, serialization time, atomic-write time, and checkpoint size.

Run `uig trace start` at any time. If the server and client are not running, the graph opens in an amber waiting state and automatically connects when they appear. Both executables observe a lightweight runtime control signal, so tracing can also attach without restarting or interrupting an active game. The monitor correlates FIFO client sends with their matching server receipts across movement, interaction, UI, inventory, combat, authentication, and system action families. Every plotted value is the one-way client-to-server response difference in milliseconds. Family toggles above the graph isolate individual action types. The summary lines show the rolling total game-action response, the measured background probe response, and **Game Response Change = Total Response Change - Background Response Change**. The active health line and `uig trace status` report sample rate, pending/expired correlations, reader backlog, threshold counts, heartbeat freshness, and server stalls. Warning and critical guides are drawn at 50 and 100 ms. Ingestion is bounded, parsers are compiled, rolling calculations are constant-time, and CSV writes are flushed in one-second batches to keep monitoring overhead low. Matched samples are recorded in `Logs/Runtime/action-trace.csv`; server-only frame-stall diagnostics remain separate in `Logs/Runtime/server-frame-stalls.csv` and are never mixed into the client/server response graph. `uig trace restart` archives the existing session under `Logs/Runtime/TraceArchive`; thirty archives of each type are retained. The shared Y axis is fixed at 0-100 ms with 10 ms major ticks and 1 ms minor ticks. Its timestamps are directly comparable for the local client/server workflow because both processes use the same computer clock. `uig trace end` flushes buffered samples, disables trace output, and closes the monitor while leaving both game processes running.

`uig client start` first establishes an app-authenticated connection, then opens the in-game login screen. Enter a username and password and choose **Create Account** on first use; the server assigns an opaque random 256-bit user ID. Later, Login accepts either the username or that user ID plus the password. The optional launcher identifier only prefills the form—it never bypasses password verification. Account verification and account files are owned only by the dedicated server; the launcher never accepts or stores account passwords.
