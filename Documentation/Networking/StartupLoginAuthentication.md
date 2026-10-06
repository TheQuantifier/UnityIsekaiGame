# Startup and Local Account Authentication

Phase 6 Group 2 adds a production startup path and server-owned local accounts to the physical client/server split.

## Player flow

1. Start the dedicated server with `uig server start`.
2. Start a client with `uig client start`. The client first completes app authentication using the local installation token.
3. Use the login screen to create or authenticate an account. The server assigns a random 256-bit user ID after account creation; usernames are never used as internal player IDs.

4. Unity's engine splash is followed by the game's startup movie.
5. The client opens the brown-and-gold login screen after app connection. For a new username, enter a password of at least eight characters and choose **Create Account**.
6. For an existing account, enter either its username or 64-character user ID plus its password and choose **Login**.
7. The login overlay remains active until the server authenticates the account and the server-authoritative player actor is ready.

`uig client <username-or-userID> start` may be used to prefill the login identifier for a named tracked client. The identifier is not a credential and never bypasses the password.

The startup movie can be skipped after a short guard delay with a keyboard key, the left mouse button, or a gamepad Start button. `--skip-startup-video` exists for automated tests and development builds.

## Ownership and security boundary

- Connection approval carries only a random app instance ID, build version, and shared local installation token. Account credentials are never placed in Netcode connection data.
- Account login is a second, explicit protocol step after app admission. A player session and actor do not exist before it succeeds.
- Usernames are canonicalized to lower case for lookup. The authoritative profile/session key is a server-generated random 256-bit ID, never the username.
- Each password is stored as a PBKDF2-HMAC-SHA256 verifier with a unique random 32-byte salt and 310,000 iterations. Plaintext passwords are never persisted.
- Account records are named by opaque user ID; a hashed username index resolves names without using them as filesystem paths. Both are written atomically.
- Unknown-account and incorrect-password logins use the same response, and repeated failures receive exponential per-connection throttling.
- Passwords are cleared from request objects immediately after verification.

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
