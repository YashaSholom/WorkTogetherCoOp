# Imported traveller

`CuteAlienTraveller.prefab` wraps the user's rigged FBX. Original mesh, textures and bone binding are preserved; the model has a uniform 1.65 scale. The prefab origin is the floor in Standing Idle and the pelvis/seat cushion in Seated Idle, so vehicle seat anchors are cushion-height transforms.

The editable generic controller has two looping clips and the `Seated` boolean. The seated clip bends thighs/knees and rests the hands ahead of the body. The spine/hips have subtle breathing movement. No root motion, navigation or authoritative game state is driven by animation.

`AlienPassengerPresentation` applies head gaze after animation: scans NGO's spawned player registry every 0.2 s, picks the nearest within 5 m, tracks their motor's eye position, limits yaw/pitch and smoothly returns to forward when no one is nearby. Only head rotation changes; the torso remains seated. Every peer derives this from replicated player positions, including client views and late joiners.

The previous frame's gaze is restored to its unmodified animation pose in Update, before Animator evaluation. Constant head curves may skip writes; multiplying into the existing rotation without this reset causes continuous spinning. Disable and seating changes also restore the pose. `PresentedHeadLocalRotation` records the final LateUpdate pose for editor diagnostics, since mailbox requests may run between Update and LateUpdate.

Two existing lore race entries temporarily share this visual, pending the next imported species. Model-specific animation and gaze references belong on its prefab, so a later species can use a different rig/controller without changing the visitor game flow. Traveller portraits currently share this real-model image; the session's configured false-ID image keeps portrait-mismatch cases visibly testable.

Edit these assets directly. The one-off assembly/animation authoring helper is not a retained rebuild tool.
