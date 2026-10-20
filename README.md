# BR200 dedicated servers on Edgegap

Run Photon's [BR200 Battle Royale (Photon Fusion)](https://assetstore.unity.com/packages/templates/packs/br200-battle-royale-multiplayer-photon-fusion-226753) sample as a dedicated server on [Edgegap](https://edgegap.com), one deployment per match, close to the players.

Copy this kit into your BR200 project. It doesn't change any BR200 or Fusion code, and it doesn't include any. You still get BR200 from the Unity Asset Store under your own licence.

> **Tested** on 2026-10-19 with BR200 2.1.0, Fusion 2.1.1 (build 2177) and Unity 6000.3.11f1 on Windows: a server built with this kit, a local container and Edgegap deployments in Toronto, two clients each. See [What was tested](#what-was-tested).

## What's in the kit

| Path | What it does |
|---|---|
| `Assets/EdgegapBR200/Editor/BR200ServerBuilder.cs` | Adds **Tools > Edgegap BR200**. Checks the project (IL2CPP server backend and Linux toolchain, unsafe code, BR200 scenes in the build profile, Fusion App ID, Peer Mode) and builds the Linux **Server** subtarget to `Builds/EdgegapServer/ServerBuild`, the folder the Edgegap plugin packages. You can also call it from the Unity CLI. |
| `edgegap/Dockerfile` | An Ubuntu 22.04 image that runs the server as a non-root user. Point the Edgegap plugin at it, or build it with `scripts/package.sh`. |
| `edgegap/start-server.sh` | Reads the variables Edgegap injects and passes BR200's own command-line options. It picks the Photon region, forwards stop signals and stops the deployment when the match ends. |
| `.dockerignore` | Sends only the server build to Docker, not the whole Unity project. |
| `scripts/` | Build, package and push, run locally with Edgegap's variables, and run the tests in a container. |
| `examples/` | An app version body and a deployment request for the Edgegap API. |
| `tests/` | Tests for the start script. They don't need Unity. |

## What you need

- The BR200 project from the Asset Store, opened in a Unity version it supports, with the **Linux Dedicated Server** build module.
- The server must use **IL2CPP**, BR200's default. On a Mono server, BR200's RPCs fail with `MethodAccessException` and players never get a character. To build Linux IL2CPP on Windows, add Unity's Linux toolchain in **Window > Package Manager > + > Add package by name**:
  - Unity 6000.3 and later: `com.unity.toolchain.win-x86_64-linux` and `com.unity.sdk.linux-x86_64`
  - earlier versions: `com.unity.toolchain.win-x86_64-linux-x86_64`
- A Photon Fusion App ID from the [Photon dashboard](https://dashboard.photonengine.com).
- An Edgegap account and the [Edgegap Unity plugin](https://github.com/edgegap/edgegap-unity-plugin) (*Edgegap Servers Quickstart*, written against 3.2.1). You only need it if you build from the editor.
- Docker Desktop.

## 1. Add the kit to the project

Copy `Assets/EdgegapBR200`, `edgegap/`, `scripts/`, `.dockerignore` and `.gitattributes` into the BR200 project root, next to `Assets/`.

Then in Unity:

1. In **Tools > Fusion > Realtime Settings**, paste your Fusion App ID. Note the **App Version**. Clients and server must use the same App ID, App Version and Photon region, or they won't find each other.
2. In **Tools > Fusion > Network Project Config**, set **Peer Mode** to `Single`.
3. Run **Tools > Edgegap BR200 > Check Project** and fix anything it reports.

You don't need to edit the Loader scene or the Loader script. The server's mode, address, port, session and region come from the command line.

If the console shows `Unsafe code may only appear if compiling with /unsafe` right after the first import, close and reopen the project. BR200's Project Settings already allow unsafe code, but the first import can compile before they're applied.

## 2. Build, package and upload

**From the Edgegap plugin** (Tools > Edgegap Server Hosting):

1. Run **Tools > Edgegap BR200 > Build Linux Server**. It checks the project, fixes *Allow 'unsafe' Code* if needed, and builds to `Builds/EdgegapServer`. It then sets the editor's standalone subtarget back to what it was, so your next client build isn't a headless server.
2. In the plugin's **3. Containerize Your Server** step, click **Path To Dockerfile** and select `edgegap/Dockerfile`. Keep **Relative Build Path** at `Builds/EdgegapServer`. Containerize, then upload.
3. When the plugin creates the app version, keep port `7777` / UDP.

**From a terminal** (Git Bash on Windows works):

```bash
scripts/build-server.sh
```

```bash
scripts/package.sh registry.edgegap.com/<your-registry-project>/br200
```

Then create the app version in the dashboard or with [`examples/app-version.json`](examples/app-version.json) (`POST /v1/app/{app}/version`).

Use these app version settings in both cases:

- **Port:** 7777, UDP, named `gameport`.
- **Restart policy:** `never`. The deployment ends when the match ends and the server exits.
- **Max duration:** a ceiling a little above your longest match (60 minutes in the example).
- **CPU and memory:** the example's 1 vCPU / 2 GB is a starting point. A 200-player match has not been sized yet.

## 3. Test locally first

```bash
scripts/run-local.sh eu
```

This runs the image on `127.0.0.1:7777/udp` with the same `ARBITRIUM_*` variables a real deployment gets, and prints the session name. Join with **Option B** below using that session name and the region you passed, from two editors or two client builds. Expect CT **Relayed** in this local test: a client on the same machine can't reach Docker Desktop's NAT address directly. On Edgegap the same image gives Direct.

## 4. Start a match and connect

Start a deployment from the plugin, the dashboard or `POST /v2/deployments` ([`examples/deploy-request.json`](examples/deploy-request.json)). Pass the players' public IPs so Edgegap places the server near them. The deployment's FQDN, for example `0a1b2c3d4e5f.pr.edgegap.net`, is also the BR200 session and server name.

The server's first log lines show where it binds, its public address and the Photon region it chose:

```
[edgegap-br200] deployment=0a1b2c3d4e5f session=0a1b2c3d4e5f.pr.edgegap.net bind=0.0.0.0:7777/udp public=203.0.113.10:31502
[edgegap-br200] photon_region=eu (nearest to Frankfurt (50.11, 8.68)) max_players=200 mode=default scene=default
```

**Option A: Menu.** Open `TPSBR/Scenes/Menu`, play, choose the same region, and join the server from the list.

**Option B: Standalone Manager auto-join.** Open the scene set as the Loader's **Batch Mode Scene**. In the **Standalone Manager**, set Game Mode `Client`, set Server Name and Session Name to the FQDN, and set Region to the server's region. Then play.

BR200 starts a **120-second waiting timer when the server boots**. Players who join after it runs out count as eliminated. Deploy when the group is ready and connect clients as soon as the deployment is Ready.

**Check the connection type.** In a connected client, press **Tab**. The overlay's top-left corner shows **CT** (Connection Type). It should say **Direct**. **Relayed** means the client reaches the server through a Photon relay, which adds latency. See [Direct connection](#direct-connection).

## Runtime variables

Edgegap sets the `ARBITRIUM_*` variables. You set the `BR200_*` ones on the app version (every match) or on the deployment request (this match only).

| Variable | Default | BR200 option |
|---|---|---|
| `BR200_SESSION` | The deployment FQDN, `<ARBITRIUM_REQUEST_ID>.pr.edgegap.net` | `-sessionName` |
| `BR200_SERVER_NAME` | Same as the session | `-serverName` |
| `BR200_REGION` | `auto`: the Photon region nearest the deployment. `best`: let Fusion ping. Or a code such as `eu`. | `-region` |
| `BR200_REGION_ALLOWLIST` | All Photon regions. Comma-separated codes that `auto` may pick from. | |
| `BR200_MAX_PLAYERS` | `200` (1 to 200) | `-maxPlayers` |
| `BR200_MODE` | BR200's default. `battleRoyale`, `deathmatch` or `elimination`. | `-battleRoyale` / `-deathmatch` / `-elimination` |
| `BR200_SCENE` | The Loader's Batch Mode Scene | `-scene` |
| `BR200_LOBBY` | BR200's default | `-lobby` |
| `BR200_FPS` | Fusion's tick rate | `-fps` |
| `BR200_RECORD` | `false`. `true` writes session stats to `/data`. | `-recordSession -dataPath /data` |
| `BR200_EXTRA_ARGS` | None. Other options, appended last. | |
| `BR200_SELF_STOP` | `true`. Stop the deployment when BR200 exits. | |

The script always passes `-dedicatedServer -ip 0.0.0.0 -port 7777` (see [Direct connection](#direct-connection)). It reads the ports from `ARBITRIUM_PORTS_MAPPING`, the first UDP port whatever its name, so a port the plugin named `Game Port` works too. BR200 keeps the first copy of an option it sees, so `BR200_EXTRA_ARGS` can't override the options above. The script rejects that instead of ignoring it silently.

### How the Photon region is chosen

Fusion servers and clients must be in the same Photon region. Edgegap places each deployment near its players, and `auto` puts the server in the Photon region closest to that deployment. That's usually the region the players' clients pick by ping. If your clients use a fixed region, or your Photon app only allows some regions, set `BR200_REGION` on the deployment, or restrict `auto` with `BR200_REGION_ALLOWLIST`. The Chinese mainland region (`cn`) is never picked automatically.

### Direct connection

BR200's `IP` and `Port` (the Loader's `_batchModeConfiguration`, or `-ip` / `-port`) set the address the server **binds** to: Fusion's `StartGameArgs.Address`. The script binds `0.0.0.0:7777`. Fusion then finds the deployment's public address on its own, and clients connect **Direct**, even though Edgegap gives players a different public port such as `31722`.

What we measured with two clients (Tab > CT):

| Server `IP` | Result |
|---|---|
| `0.0.0.0` (this kit) | **Direct** on Edgegap, 11 to 126 ms from a client in eastern Canada to a Toronto deployment. |
| `127.0.0.1` | The server starts, but clients are **Relayed** (about 235 ms through Photon). Nothing outside the container can reach the loopback address. |
| The deployment's public IP | The server **doesn't start**: `Failed to bind socket to [Address Ip=… Port=7777]`, because the container doesn't own that address. Clients get `GameNotFound`. |

So leave `IP` empty or `0.0.0.0`. Don't set it to `127.0.0.1` or to Edgegap's public IP.

### End of match

When BR200 exits, the script calls the deployment's own self-stop URL (`ARBITRIUM_DELETE_URL`), so a finished match stops billing right away. With restart policy `never`, the deployment would end anyway. When Edgegap stops the deployment (SIGTERM), the script forwards the signal to BR200 and makes no extra call. The script never prints the environment, so the self-stop token stays out of the logs.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `Unsafe code may only appear if compiling with /unsafe` right after importing BR200 | First-import ordering. Reopen the project. If it persists, enable *Allow 'unsafe' Code*; Build Linux Server turns it on. |
| `MethodAccessException ... CheckInvokeRpc` in the server log | The server was built with Mono. Use IL2CPP. |
| `No Toolchain found for host platform` or `No Linux sysroot found` | The Linux toolchain packages are missing. See [What you need](#what-you-need). |
| Client gets `GameNotFound` | The App Version or Photon region differs between client and server. Compare with the server's `photon_region=` log line. |
| Client gets `InvalidAuthentication` | Wrong or missing Fusion App ID in the build. |
| Tab overlay shows CT **Relayed** | The server binds `127.0.0.1`. See [Direct connection](#direct-connection). |
| Deployment log shows `Failed to bind socket` | `IP` is set to an address the container doesn't own. Leave it empty or `0.0.0.0`. |
| Server isn't in the Menu list | The client's region differs from the server's, or the 120-second waiting timer has already run out. |
| Deployment exits at once with `error: ...` | A `BR200_*` value is invalid. The log line says which one. |
| `ServerBuild: not found` in the logs | The image was built from a different folder. Rebuild to `Builds/EdgegapServer`, or pass `SERVER_BUILD_PATH`, and remove that folder from `.dockerignore`'s exclusions. |
| `bad interpreter` / `\r` errors | Line endings. Keep `.gitattributes`. The Dockerfile also strips CRLF. |

## Tests

```bash
bash tests/start-server.test.sh
```

```bash
scripts/test-in-container.sh
```

The first command runs the start-script tests against a stand-in server binary (46 checks). They cover option mapping, the bind address, port lookup, region choice, rejection of bad values, self-stop, token hygiene and signal forwarding. The second runs the same tests in `ubuntu:22.04`, the server's base image.

## What was tested

BR200 2.1.0, Fusion 2.1.1 build 2177, Unity 6000.3.11f1, Windows 11, Docker Desktop. The clients were Windows IL2CPP builds running headless (`-batchmode -nographics -client -sessionName <FQDN> -region <code> -generateInput`). CT was read from `runner.CurrentConnectionType`, the value the Tab overlay shows.

- **Build:** Check Project and Build Linux Server produced an IL2CPP Linux server in 56 minutes, almost all of it IL2CPP (a Mono build takes 2 minutes but breaks RPCs; see [What you need](#what-you-need)).
- **Local container:** two clients joined with no exceptions. They show CT **Relayed** there, because a client on the same machine can't reach Docker Desktop's NAT address. That only happens locally.
- **Edgegap:** deployments in Toronto. The kit's region choice picked `cae`. Two clients joined each one with CT Direct (11 to 126 ms in Toronto; 14 to 15 ms in Newark, where the kit picked `us`). In a two-player Battle Royale with no exceptions on server or clients, one client left, the other got the Game Over screen, and the deployment stopped on its own 2.5 minutes after it started.
- **Not tested yet:** 200 players, CPU and memory sizing for a full match, maps other than GenArea7, the Menu UI join path, and macOS or Linux build hosts.

## Licence

MIT, see [LICENSE](LICENSE). BR200, Photon Fusion and their assets aren't part of this repository. They remain under the Unity Asset Store EULA and Photon's terms.
