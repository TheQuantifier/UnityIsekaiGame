# Startup and Local Account Authentication

Phase 6 Group 2 adds a production startup path and server-owned local accounts to the physical client/server split.

## Player flow

1. Start the dedicated server with `uig server start`.
2. Start a client with `uig client <clientID> start`.
3. Unity's engine splash is followed by the game's startup movie.
4. The client opens the brown-and-gold login screen. The launcher-provided client ID is offered as the username, but it can be edited.
5. For a new username, enter a password of at least eight characters and choose **Create Account**.
6. For an existing username, enter its password and choose **Login**.
7. The login overlay remains active until the server authenticates the account and the server-authoritative player actor is ready.

The startup movie can be skipped after a short guard delay with a keyboard key, the left mouse button, or a gamepad Start button. `--skip-startup-video` exists for automated tests and development builds.

## Ownership and security boundary

- The client sends a versioned connection payload containing the requested account operation and credentials. It never writes account records.
- The dedicated server validates the protocol/build/shared-launch token before authenticating the account.
- Usernames are canonicalized to lower case and become the authoritative player identity used by the existing profile/session systems.
- Each password is stored as a PBKDF2-HMAC-SHA256 verifier with a unique random 32-byte salt and 210,000 iterations. Plaintext passwords are never persisted.
- Account filenames are stable SHA-256 identifiers rather than raw usernames, and records are written atomically.
- Login failures use a common response for an unknown username or incorrect password.
- The password is cleared from the request object and Netcode connection data as soon as admission finishes.

Account records are stored below the server build's `Application.persistentDataPath/ServerData/Accounts` directory. They are intentionally outside the repository and client project.

The current transport is a loopback-only local development transport. Before exposing it to a network, add a mutually authenticated encrypted channel (or a password-authenticated key exchange) so credentials are never transmitted in cleartext over an untrusted network.

## Assets and scenes

- Startup scene: `Projects/Client/Assets/_Project/Scenes/Production/Startup/StartupScene.unity`
- Startup movie: `Projects/Client/Assets/StreamingAssets/Startup/startup_scene.mp4`
- Login artwork: `Projects/Client/Assets/_Project/Presentation/Login/Resources/Login/backgroundimage.jpg`

The startup scene is first in Client Build Settings and loads `PrototypeScene`, which remains the vertical slice. The server project does not include the movie, artwork, or login UI.

## Verification

- Client EditMode tests cover protocol validation, login image cover-cropping, startup Build Settings, and client-side UI boundaries.
- Server EditMode tests cover account creation, case-normalized login, duplicate usernames, incorrect passwords, and ensuring persisted JSON does not contain the plaintext password.
- The production client and dedicated-server build commands verify that each physical project contains only its intended runtime side.
