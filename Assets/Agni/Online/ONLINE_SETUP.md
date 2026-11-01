# Agni Royale — turning on REAL online (Unity Gaming Services, free tier)

The game already contains the online code (friends by Name#1234, requests, presence, squad invites, room codes over
Lobby + Relay). It stays in demo/LAN mode until the project is linked to a Unity Cloud project. One-time steps:

1. Go to https://cloud.unity.com and sign in with the Unity account that owns the game.
2. Projects → **Create project** → name it "Agni Royale" → open it → copy the **Project ID** (Settings / Project ID).
3. Inside the project turn on these products (each has a "Get started" / "Enable" button, all free tier):
   - **Authentication** (Anonymous sign-in is on by default)
   - **Friends**
   - **Multiplayer → Lobby**
   - **Multiplayer → Relay**
   (Unity can ask for billing details before Lobby/Relay go live. Pay-as-you-go charges only above the free allowance.)
4. In the Unity editor: either
   - Edit ▸ Project Settings ▸ **Services** → "Link to existing project" → pick Agni Royale, **or**
   - paste the Project ID into `Assets/Agni/Online/Resources/AgniOnline/AgniOnlineConfig.json` (`"cloudProjectId"`)
     and click menu **Agni Royale ▸ Online ▸ Link Cloud Project**, then restart the editor.
5. Build the APK again. On launch the lobby DOST panel shows **ONLINE** and your own ID like `Sujon#4821`.

Test with two phones (any internet): phone A → DOST → ROOM CODE → ROOM BANAO → share the code; phone B → ROOM CODE →
type the code → JUDO. Or add each other with ADD ID (Name#1234) and use INVITE.

Later (not needed for the first test): Google sign-in (OAuth client + SHA-1 of the release keystore), a dedicated
server box for 50 real humans in one match (until then the squad leader's phone hosts the squad + bots).
