# BR200 dedicated servers on Edgegap

Run Photon's [BR200 Battle Royale (Photon Fusion)](https://assetstore.unity.com/packages/templates/packs/br200-battle-royale-multiplayer-photon-fusion-226753) sample as a dedicated server on [Edgegap](https://edgegap.com), one deployment per match, close to the players.

Copy this kit into your BR200 project. It doesn't change any BR200 or Fusion code, and it doesn't include any. You still get BR200 from the Unity Asset Store under your own licence.

> **Status:** the start script and image are tested (see [Tests](#tests)). A full BR200 build and an Edgegap run with this kit have not been done yet. The [checklist](#before-a-release) lists what's left.

## What's in the kit

| Path | What it does |
|---|---|
| `Assets/EdgegapBR200/Editor/BR200ServerBuilder.cs` | Adds **Tools > Edgegap BR200**. Checks the project (unsafe code, BR200 scenes in the build profile, Fusion App ID, Peer Mode) and builds the Linux **Server** subtarget to `Builds/EdgegapServer/ServerBuild`, the folder the Edgegap plugin packages. You can also call it from the Unity CLI. |
| `edgegap/Dockerfile` | An Ubuntu 22.04 image that runs the server as a non-root user. Point the Edgegap plugin at it, or build it with `scripts/package.sh`. |
| `edgegap/start-server.sh` | Reads the variables Edgegap injects and passes BR200's own command-line options. It picks the Photon region, forwards stop signals and stops the deployment when the match ends. |
| `.dockerignore` | Sends only the server build to Docker, not the whole Unity project. |
| `scripts/` | Build, package and push, run locally with Edgegap's variables, and run the tests in a container. |
| `examples/` | An app version body and a deployment request for the Edgegap API. |
| `tests/` | Tests for the start script. They don't need Unity. |

## What you need

- The BR200 project from the Asset Store, opened in a Unity version it supports, with the **Linux Dedicated Server** build module.
- A Photon Fusion App ID from the [Photon dashboard](https://dashboard.photonengine.com).
- An Edgegap account and the [Edgegap Unity plugin](https://github.com/edgegap/edgegap-unity-plugin) (*Edgegap Servers Quickstart*, written against 3.2.1). You only need it if you build from the editor..
- Docker Desktop.

## 1. Add the kit to the project

Copy `Assets/EdgegapBR200`, `edgegap/`, `scripts/`, `.dockerignore` and `.gitattributes` into the BR200 project root, next to `Assets/`.

Then in Unity:

1. In **Tools > Fusion > Realtime Settings**, paste your Fusion App ID. Note the **App Version**. Clients and server must use the same App ID, App Version and Photon region, or they won't find each other.
2. In **Tools > Fusion > Network Project Config**, set **Peer Mode** to `Single`.
3. Run **Tools > Edgegap BR200 > Check Project** and fix anything it reports.

You don't need to edit the Loader scene or the Loader script. The server's mode, address, port, session and region come from the command line.

## 2. Build, package and upload

**From the Edgegap plugin** (Tools > Edgegap Server Hosting):

1. Run **Tools > Edgegap BR200 > Build Linux Server**. It checks the project, fixes *Allow 'unsafe' Code* if needed, and builds to `Builds/EdgegapServer`.
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

This runs the image on `127.0.0.1:7777/udp` with the same `ARBITRIUM_*` variables a real deployment gets, and prints the session name. In two editors, join with **Option B** below using that session name and the region you passed.

## 4. Start a match and connect

Start a deployment from the plugin, the dashboard or `POST /v2/deployments` ([`examples/deploy-request.json`](examples/deploy-request.json)). Pass the players' public IPs so Edgegap places the server near them. The deployment's FQDN, for example `0a1b2c3d4e5f.pr.edgegap.net`, is also the BR200 session and server name.

The server's first log lines show its public address, the connection mode and the Photon region it chose:

```
[edgegap-br200] deployment=0a1b2c3d4e5f session=0a1b2c3d4e5f.pr.edgegap.net port=7777/udp public=203.0.113.10:31502
[edgegap-br200] connection=direct -ip 203.0.113.10 -port 7777
[edgegap-br200] photon_region=eu (nearest to Frankfurt (50.11, 8.68)) max_players=200 mode=default scene=default
```

**Option A: Menu.** Open `TPSBR/Scenes/Menu`, play, choose the same region, and join the server from the list.

**Option B: Standalone Manager auto-join.** Open the scene set as the Loader's **Batch Mode Scene**. In the **Standalone Manager**, set Game Mode `Client`, set Server Name and Session Name to the FQDN, and set Region to the server's region. Then play.

BR200 starts a **120-second waiting timer when the server boots**. Players who join after it runs out count as eliminated. Deploy when the group is ready and connect clients as soon as the deployment is Ready.

**Check the connection type.** In a connected client, press **Tab**. The overlay's top-left corner shows **CT** (Connection Type). It should say **Direct**. **Relay** means the client reaches the server through a Photon relay, which adds latency. See [Direct connection](#direct-connection).

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
| `BR200_CONNECTION` | `direct`: pass the deployment's public IP. `relay`: bind `0.0.0.0` and let Photon relay every client. | `-ip` |
| `BR200_DIRECT_PORT` | `bind`: pass the port the server binds to (7777). `public`: pass Edgegap's public port. | `-port` |
| `BR200_PUBLIC_IP` | `ARBITRIUM_PUBLIC_IP` | `-ip` (direct) |

The script always passes `-dedicatedServer`, `-ip` and `-port` (see [Direct connection](#direct-connection)). It reads the ports from `ARBITRIUM_PORTS_MAPPING`, the first UDP port whatever its name, so a port the plugin named `Game Port` works too. BR200 keeps the first copy of an option it sees, so `BR200_EXTRA_ARGS` can't override the options above. The script rejects that instead of ignoring it silently.

### How the Photon region is chosen

Fusion servers and clients must be in the same Photon region. Edgegap places each deployment near its players, and `auto` puts the server in the Photon region closest to that deployment. That's usually the region the players' clients pick by ping. If your clients use a fixed region, or your Photon app only allows some regions, set `BR200_REGION` on the deployment, or restrict `auto` with `BR200_REGION_ALLOWLIST`. The Chinese mainland region (`cn`) is never picked automatically.

### Direct connection

Photon Fusion connects each client straight to the server (**Direct**) only when the server is given its public IP address and port. BR200 takes them from its `IP` and `Port` settings, the same settings as the Loader's `_batchModeConfiguration`, and from `-ip` / `-port` on the command line. Without them, every client is relayed through Photon. A relayed match works, but each packet takes an extra hop.

By default the script passes the deployment's public IP (`ARBITRIUM_PUBLIC_IP`) and the port the server binds to (7777), as Photon recommends. Check the result with **Tab > CT** on a client:

| CT shows | What to do |
|---|---|
| Direct | Nothing. |
| Relay | Edgegap gives players a different public port, such as `31502`, than the port the server binds to. Try `BR200_DIRECT_PORT=public` on a test deployment. If the server then stops accepting players, go back to `bind` and keep relayed connections until BR200 can advertise a separate public port. |
| The server doesn't start, or no client joins | BR200 may be trying to bind the public IP. Set `BR200_CONNECTION=relay` to get the previous behaviour back. |

`scripts/run-local.sh` runs relayed by default, because a Docker Desktop container doesn't own a public IP. To try Direct on your network, set `BR200_CONNECTION=direct BR200_PUBLIC_IP=<your LAN IP>`.

### End of match

When BR200 exits, the script calls the deployment's own self-stop URL (`ARBITRIUM_DELETE_URL`), so a finished match stops billing right away. With restart policy `never`, the deployment would end anyway. When Edgegap stops the deployment (SIGTERM), the script forwards the signal to BR200 and makes no extra call. The script never prints the environment, so the self-stop token stays out of the logs.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Console errors right after importing BR200 | *Allow 'unsafe' Code* is off. Check Project reports it, and Build Linux Server turns it on. |
| Client gets `GameNotFound` | The App Version or Photon region differs between client and server. Compare with the server's `photon_region=` log line. |
| Client gets `InvalidAuthentication` | Wrong or missing Fusion App ID in the build. |
| Tab overlay shows CT **Relay** | The server didn't get a usable public address. See [Direct connection](#direct-connection). |
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

The first command runs the start-script tests against a stand-in server binary (54 checks). They cover option mapping, Direct and relay addressing, port lookup, region choice, rejection of bad values, self-stop, token hygiene and signal forwarding. The second runs the same tests in `ubuntu:22.04`, the server's base image.

## Before a release

- [ ] Build BR200 with **Build Linux Server** and record the BR200, Fusion and Unity versions here.
- [ ] Two clients join a local container (`scripts/run-local.sh`).
- [ ] Two clients join an Edgegap deployment with `BR200_REGION=auto`, through the Menu and through the Standalone Manager.
- [ ] **Tab > CT** shows **Direct** on those clients. Record which `BR200_DIRECT_PORT` value gave it.
- [ ] A match ends and the deployment stops on its own.
- [ ] Size CPU and memory for a full match.

## Licence

MIT, see [LICENSE](LICENSE). BR200, Photon Fusion and their assets aren't part of this repository. They remain under the Unity Asset Store EULA and Photon's terms.
