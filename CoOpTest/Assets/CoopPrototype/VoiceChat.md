# Proximity voice and shared settings

Start from MainMenu. Voice uses the existing Netcode connection (Direct/IP or Steam transport), without Vivox/UGS credentials or another package. The preserved legacy CoopWorkshop direct-play scene does not contain the frontend session scope; use MainMenu → GameWorld for this feature.

## Controls

- Hold **T** to transmit (default). Only the focused instance captures a microphone. Release T to stop capture. Voice is not recorded to disk.
- **Escape** or the **Settings & voice** HUD button opens Settings during gameplay. Opening it releases the cursor, blocks movement/look/interaction input and pauses transmission. It does not pause the shared world. Save & back/Escape closes it; click the world to resume control.
- Settings is also available from the home screen and party lobby.
- Choose microphone/System default; Refresh microphone devices after plugging one in. Missing devices display an error instead of silently selecting another input.
- Optional open mic uses microphone gain, an RMS activation threshold and a short release tail. Higher threshold ignores quieter sounds.
- Enable voice, mute microphone, deafen (input and output), incoming voice volume, and master volume are separate controls.
- Test microphone opens a **local level meter only**. It never sends test microphone audio to peers, and stops when Settings closes.
- The host can change hearing distance (3–40 metres) and enable/disable lobby-wide chat. Changes replicate to current and subsequently connected peers. These are session rules; saved scene defaults live on Session Scope → ProximityVoice.
- Names of currently heard speakers appear in the gameplay HUD. Lobby chat is non-positional; gameplay chat is positional and fades linearly from fullVolumeDistance to hearingDistance.

Local preferences persist through PlayerPrefs. MPPM editors share a preference store, though each running editor has its own in-memory settings. Use headphones: this implementation has no acoustic echo cancellation, automatic gain control or noise suppression. Windows desktop apps must have microphone access enabled. Physical device selection/capture should be checked on the actual machines being used.

## Architecture and editable settings

MenuCompositionRoot injects GameFlow and VoicePreferences into the saved ProximityVoice component. The shared SettingsPanel receives these same dependencies; no global voice singleton is introduced.

- `MicrophoneCapture`: microphone lifetime, bounded ring-buffer reads, mono downmix/resampling to 16kHz.
- `VoiceCodec`: independent 20ms IMA ADPCM speech frames (164 bytes, approximately 8.2KB/s per active speaker before network headers).
- `ProximityVoice`: bounded NGO named messages, connection lifecycle, host rules, sender identity from transport, rate/sequence validation and recipient filtering using server player positions.
- `VoicePlayback`: a bounded audio-thread-safe jitter queue and local AudioSource at each remote player's head. Old/reordered packets are discarded; missing samples become silence. Each packet has its own decoder state, so packet loss cannot corrupt later packets.
- `VoicePreferences`: local microphone/mute/mode/volume settings; `SettingsPanel`: bindings and live adjustments; `MenuPresenter`: screen/navigation and gameplay input blocking.

Audio packets use unreliable delivery to avoid retransmitting stale speech. Rule updates use reliable delivery, including a snapshot on join. Clients cannot change the host's hearing radius. The server does not relay out-of-range speech, rather than merely turning down its volume on distant clients. No self-echo is sent. Settings/capture and streaming outputs stop or clean up on disconnect, scene transition and session teardown.

The host still receives incoming voice to relay it; proximity is a gameplay rule, not end-to-end confidentiality. Distance has no wall occlusion or reverb simulation. This is a small-party speech implementation; a dedicated voice codec/service with echo cancellation and packet-loss concealment is a future upgrade if broader platform/audio requirements emerge.

## Validation workflow

Prefer MPPM Player 2 with Direct/IP. No rebuild is needed. Test real microphones/headphones with two computers for the final acoustic check; two local editors can contend for one device and produce speaker feedback.

`Coop Prototype > Voice > Validate Speech Codec` exercises actual codec round-trip distortion, malformed block rejection and independent silence decoding. The editor mailbox supports `voicecodec`, `voicetone`, `voicestatus`, `voicerange`, `voicemute` and `voicedeafen`. `voicetone` sends a generated 400Hz signal for at most three seconds through the real transport/decoder/playback path and never opens a microphone. These probes are editor/development-only.

For two-peer regression: connect in lobby, check voice/rule snapshot, start GameWorld, send a tone nearby then outside the radius; check receive/output counters on the other peer; test reverse direction, mute and deafen, client rejection of host-rule changes, settings open/close, and reconnect. `outputSamples` confirms consumption by Unity's audio callback, not a human listening test.

API references: [NGO custom messages](https://mp-docs.dl.it.unity3d.com/netcode/1.12.0/advanced-topics/message-system/custom-messages/) and [Unity streaming audio callback](https://docs.unity.com/en-us/engine/6000.5/script-reference/unityengine/audioclip/pcmreadercallback). Installed NGO 2.7 source was used to verify concrete signatures.

## Executed checks (2026-09-28)

Unity compilation and codec validation passed. A two-frequency test signal reconstructed at 36.7dB SNR; malformed ADPCM index and independent silence-frame tests passed.

`Tools/Test-VoiceEditors.ps1` passed ten checks in main editor + MPPM Player 2: nearby transmission in both directions, audio callback consumption, out-of-range relay exclusion, client rule-change rejection, host rule replication, increased-range delivery, deafen, mute and gameplay Settings access. Results: `TestResults/Voice/results.txt`. Additional checks confirmed rules on lobby join, positional AudioSources (`spatialBlend=1`), live volume-slider changes reaching the source, the range slider reaching the remote peer, microphone-idle state, cleanup back to MainMenu and re-host/reconnect. Settings was visually checked using Unity's Game view captures, including its lower voice controls; the gameplay interaction HUD is hidden while Settings is open.

No physical microphone was captured during automated tests; signals were generated locally. Real microphone/device acoustic quality, subjective latency, adverse network conditions and two-account Steam audio remain unverified. No standalone rebuild was used. The unrelated pre-existing Unity AI/license-service entitlement errors are still present in editor logs.
