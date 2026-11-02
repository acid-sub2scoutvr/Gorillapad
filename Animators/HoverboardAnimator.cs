using GorillaLocomotion;
using HarmonyLib;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Patches;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Hoverboards, ridden with the game's own hoverboard physics. While walking in a hover area, hold
    /// either mouse button near a board to grab it with the right hand (the same grab path as the
    /// glider). The game parents the board to that hand with a fixed hold offset and steers by the
    /// board's angle, so every tick the hand is angled so the board lies under you pointing where you
    /// face, rolled by A/D to carve. W pushes you along the board (up to the game's own max paddle
    /// speed) while the free hand animates paddling, Space hops, letting go of the mouse button drops
    /// the board. Dropped boards are grabbed through their handle's own OnGrab.
    /// Logs diagnostics twice a second while riding, for tuning.
    /// </summary>
    public class HoverboardAnimator : AnimatorBase
    {
        // ---- Tuning ----
        const float GrabReach = 1.3f;           // handle within this distance of the head
        const float GrabTimeout = 1.5f;
        const float OnHandleDistance = .1f;
        const float SqueezePeriod = .2f;
        // Board this far below the body. The game only hovers when the board is within its ideal hover
        // height of the ground (logged as "hover height"), and it holds the board at a fixed offset from
        // the head, so this mostly sets posture; too low and it ends up in the ground.
        const float BoardDrop = .3f;
        const float HopCooldown = .4f;
        const float CarveDegrees = 25f;         // A/D rolls the board this much
        const float PaddlePeriod = .55f;        // seconds per paddle stroke
        const float PaddleReach = .35f;         // stroke from this far ahead of the shoulder to as far behind
        const float PaddlePress = .04f;         // push this far into the ground during the stroke
        const float PaddleAcceleration = 6f;    // m/s^2 that W adds along the board
        const float DefaultMaxPaddleSpeed = 6f; // if the game's value can't be read
        const float HopSpeed = 4f;              // m/s upward for Space
        const float RegrabDelay = .5f;
        const float LogInterval = .5f;

        static readonly AccessTools.FieldRef<GTPlayer, HoverboardVisual> visualRef =
            AccessTools.FieldRefAccess<GTPlayer, HoverboardVisual>("hoverboardVisual");

        public AnimatorBase returnTo;
        InteractionPoint handle;
        float grabStart, paddlePhase, nextLog, lastHop;
        bool wasHeld, triedDirectGrab;

        static InteractionPoint[] handles = new InteractionPoint[0];
        static float lastSearch = -100, lastRelease = -100;

        static float Scale => GTPlayer.Instance.scale;
        static bool GrabHeld => Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
        static HoverboardVisual Visual { get { try { return visualRef(GTPlayer.Instance); } catch { return null; } } }
        static bool Riding => GTPlayer.Instance.enableHoverMode && Visual && Visual.IsHeld;
        static bool HoveredLastFrame
        {
            get { try { return Traverse.Create(GTPlayer.Instance).Field("didHoverLastFrame").GetValue<bool>(); } catch { return false; } }
        }

        static bool IsBoardHandle(InteractionPoint point) =>
            point && (point.Holdable is HoverboardHandle || point.Holdable is FreeHoverboardHandle);

        /// <summary>Called while walking; grabs a nearby hoverboard while a mouse button is held.</summary>
        public static void CheckGrab()
        {
            var plugin = Plugin.Instance;
            if (!plugin.IsWalking || !GrabHeld || Time.time - lastRelease < RegrabDelay) return;
            if (!GTPlayer.Instance.isHoverAllowed) return; // the game only lets you grab boards in hover areas

            if (Time.time - lastSearch > 5)
            {
                handles = FindObjectsByType<InteractionPoint>(FindObjectsSortMode.None).Where(IsBoardHandle).ToArray();
                lastSearch = Time.time;
            }
            Vector3 head = GTPlayer.Instance.headCollider.transform.position;
            float bestDistance = GrabReach * Scale;
            InteractionPoint best = null;
            foreach (var candidate in handles)
            {
                if (!candidate || !candidate.isActiveAndEnabled) continue;
                float distance = candidate.transform.position.Distance(head);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
            if (!best) return;

            var animator = plugin.hoverboardAnimator as HoverboardAnimator;
            animator.returnTo = Rig.Instance.Animator;
            animator.handle = best;
            Rig.Instance.Animator = animator;
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            grabStart = Time.time;
            wasHeld = false;
            triedDirectGrab = false;
            Logging.Info("Hoverboard: reaching for", handle ? handle.transform.GetPath() : "nothing");
        }

        public override void Cleanup()
        {
            base.Cleanup(); // releases grip and hides the controllers again, which drops the board
            leftHand.followRate = rightHand.followRate = .1f;
            EquipmentInteractorPatch.injectLeft = EquipmentInteractorPatch.injectRight = null;
            lastRelease = Time.time;
        }

        void Leave(string why)
        {
            Logging.Info("Hoverboard: leaving -", why);
            Rig.Instance.Animator = returnTo ? returnTo : Plugin.Instance.walkAnimator;
        }

        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null) return;
            if (!GrabHeld)
            {
                Leave("let go");
                return;
            }
            // Hop only off the ground (or while hovering over it), and not again right away
            if (wasHeld && Keyboard.current.spaceKey.wasPressedThisFrame && Time.time - lastHop > HopCooldown
                && (rig.onGround || HoveredLastFrame))
            {
                lastHop = Time.time;
                rigidbody.linearVelocity += Gravity.Up * HopSpeed * Scale;
            }
        }

        public override void Animate()
        {
            if (rig == null) return;
            bool held = Riding;
            if (held && !wasHeld)
                Logging.Info("Hoverboard: riding (left handed:", Visual.IsLeftHanded, ")");
            if (!held && wasHeld)
            {
                // The game took it away (left the hover area, ...)
                Leave("the game dropped the board");
                return;
            }
            wasHeld = held;

            foreach (var hand in new[] { leftHand, rightHand })
            {
                // The board follows the visible hands and grabs go through the game's grab code, so the
                // real (physical) hands stay tucked in the body: down by the ground they'd count as
                // touching it and shove you around
                hand.hideControllerTransform = true;
                hand.followInBodySpace = true;
                hand.followRate = .6f;
            }

            if (!held)
            {
                Reach();
                return;
            }
            EquipmentInteractorPatch.injectLeft = EquipmentInteractorPatch.injectRight = null;

            // The game's hover physics moves the body
            rig.active = false;
            var visual = Visual;
            var holding = visual.IsLeftHanded ? leftHand : rightHand;
            var free = visual.IsLeftHanded ? rightHand : leftHand;
            // Whole hand round the board (grip alone leaves the index finger pointing)
            holding.grip = holding.trigger = true;
            free.grip = free.trigger = false;

            Steer(holding, visual);
            Paddle(free);
            Push();
            LogDiagnostics(holding, visual);
        }

        // Reach for the handle with the right hand and squeeze until the game picks it up
        void Reach()
        {
            if (!handle || Time.time - grabStart > GrabTimeout)
            {
                Leave("couldn't grab the board" + (GTPlayer.Instance.isHoverAllowed ? "" : " (not in a hover area)"));
                return;
            }
            float s = Scale;
            Vector3 point = handle.transform.position;
            rightHand.Place(point, -body.forward, Vector3.ProjectOnPlane(point - body.position, Gravity.Up));
            bool onHandle = rightHand.transform.position.Distance(point) < OnHandleDistance * s;
            bool squeezePhase = Mathf.Repeat(Time.time - grabStart, SqueezePeriod) > SqueezePeriod * .3f;
            rightHand.grip = onHandle && squeezePhase;
            EquipmentInteractorPatch.injectRight = handle;
            leftHand.targetPosition = leftHand.DefaultPosition;

            // Dropped boards don't always take the normal grab, so once the hand is on the handle, do what
            // the game's grab does for them: claim the board and hand you your own. The game would hold it
            // at whatever angle the board was to your hand (awkward for a board lying on the ground), and
            // the dropped board's own default hold puts the hand in the torso, so use the hold a fresh
            // board from the rack uses.
            if (onHandle && !triedDirectGrab && handle.Holdable is FreeHoverboardHandle freeBoard)
            {
                triedDirectGrab = true;
                try
                {
                    Logging.Info("Hoverboard: grabbing dropped board directly");
                    var board = parentFreeBoardRef(freeBoard);
                    if (hasParentBoardRef(freeBoard) && board)
                        FreeHoverboardManager.instance.SendGrabBoardRPC(board);
                    Color color = board ? board.boardColor : VRRig.LocalRig.playerColor;
                    Vector3 holdPosition = defaultHoldPosRef(freeBoard);
                    Quaternion holdRotation = defaultHoldAngleRef(freeBoard);
                    var rackHandle = FindAnyObjectByType<HoverboardHandle>(FindObjectsInactive.Include);
                    if (rackHandle)
                    {
                        holdPosition = rackHoldPosRef(rackHandle);
                        holdRotation = rackHoldAngleRef(rackHandle);
                    }
                    GTPlayer.Instance.GrabPersonalHoverboard(false, holdPosition, holdRotation, color);
                }
                catch (System.Exception e) { Logging.Exception(e); }
            }
        }

        static readonly AccessTools.FieldRef<FreeHoverboardHandle, FreeHoverboardInstance> parentFreeBoardRef =
            AccessTools.FieldRefAccess<FreeHoverboardHandle, FreeHoverboardInstance>("parentFreeBoard");
        static readonly AccessTools.FieldRef<FreeHoverboardHandle, bool> hasParentBoardRef =
            AccessTools.FieldRefAccess<FreeHoverboardHandle, bool>("hasParentBoard");
        static readonly AccessTools.FieldRef<FreeHoverboardHandle, Vector3> defaultHoldPosRef =
            AccessTools.FieldRefAccess<FreeHoverboardHandle, Vector3>("defaultHoldPosRight");
        static readonly AccessTools.FieldRef<FreeHoverboardHandle, Quaternion> defaultHoldAngleRef =
            AccessTools.FieldRefAccess<FreeHoverboardHandle, Quaternion>("defaultHoldAngleRight");
        static readonly AccessTools.FieldRef<HoverboardHandle, Vector3> rackHoldPosRef =
            AccessTools.FieldRefAccess<HoverboardHandle, Vector3>("defaultHoldPosRight");
        static readonly AccessTools.FieldRef<HoverboardHandle, Quaternion> rackHoldAngleRef =
            AccessTools.FieldRefAccess<HoverboardHandle, Quaternion>("defaultHoldAngleRight");

        // Holding W pushes you along the board while on or near the ground, up to the game's own max
        // paddle speed. (Paddling in the game is the hand pushing on the ground, which these hands can't
        // do.) The game's hover physics takes it from there: hovering, carving, grinding.
        void Push()
        {
            if (InputHandler.inputDirection.z <= 0) return;
            var traverse = Traverse.Create(GTPlayer.Instance);
            bool nearGround = rig.onGround || traverse.Field("hasHoverPoint").GetValue<bool>();
            if (!nearGround) return;
            float s = Scale;
            float maxSpeed = traverse.Field("hoverMaxPaddleSpeed").GetValue<float>();
            if (maxSpeed <= 0) maxSpeed = DefaultMaxPaddleSpeed;
            Vector3 facing = Gravity.Flatten(body.forward).normalized;
            float along = Vector3.Dot(rigidbody.linearVelocity, facing);
            if (along >= maxSpeed * s) return;
            rigidbody.linearVelocity += facing * Mathf.Min(PaddleAcceleration * s * Time.deltaTime, maxSpeed * s - along);
        }

        // Angle the holding hand so the board (held at a fixed offset from the rig's hand) lies under
        // the body, pointing where you face, rolled by A/D to carve. The rig's hand follows the hand
        // driver with an offset of its own, so both offsets are measured live rather than guessed.
        void Steer(HandDriver holding, HoverboardVisual visual)
        {
            float s = Scale;
            Vector3 up = Gravity.Up;
            Vector3 facing = Gravity.Flatten(body.forward).normalized;
            if (facing == Vector3.zero) return;
            float carve = -InputHandler.inputDirection.x * CarveDegrees;
            Quaternion boardRotation = Quaternion.AngleAxis(carve, facing) * Quaternion.LookRotation(facing, up);
            Vector3 boardPosition = body.position - up * BoardDrop * s;

            // Board = rig hand * nominal offset, so the rig hand needs to be here
            Quaternion rigHandRotation = boardRotation * Quaternion.Inverse(visual.NominalLocalRotation);
            Transform rigHand = RigHand(holding.isLeft);
            Vector3 rigHandPosition = boardPosition - rigHandRotation * Vector3.Scale(visual.NominalLocalPosition, rigHand ? rigHand.lossyScale : Vector3.one);

            // The rig hand = hand driver * its own offset, so the hand driver needs to be here
            Quaternion driverRotation = rigHandRotation;
            Vector3 driverPosition = rigHandPosition;
            if (rigHand)
            {
                Quaternion rotationOffset = Quaternion.Inverse(holding.transform.rotation) * rigHand.rotation;
                driverRotation = rigHandRotation * Quaternion.Inverse(rotationOffset);
                driverPosition = rigHandPosition - (rigHand.position - holding.transform.position);
            }
            holding.targetPosition = driverPosition;
            // Aim far along the direction so the hand's angle doesn't depend on where it is yet
            holding.lookAt = driverPosition + driverRotation * Vector3.forward * 100;
            holding.up = driverRotation * Vector3.up;
        }

        static Transform RigHand(bool left)
        {
            var rig = GorillaTagger.Instance.offlineVRRig;
            var map = left ? rig.leftHand : rig.rightHand;
            return map != null ? map.rigTarget : null;
        }

        // Holding W: the free hand plants on the ground ahead, sweeps back along it, then lifts and
        // comes forward again. The game turns hand-on-ground movement into speed.
        void Paddle(HandDriver hand)
        {
            float s = Scale;
            Vector3 up = Gravity.Up;
            Vector3 facing = Gravity.Flatten(body.forward).normalized;
            bool paddling = InputHandler.inputDirection.z > 0;
            if (!paddling)
            {
                paddlePhase = 0;
                hand.Place(hand.DefaultPosition, up, facing);
                return;
            }
            paddlePhase = Mathf.Repeat(paddlePhase + Time.deltaTime / PaddlePeriod, 1);

            Vector3 shoulder = hand.DefaultPosition;
            Vector3 ground = shoulder - up * 2 * s;
            if (Physics.Raycast(shoulder + up * .3f * s, -up, out RaycastHit hit, 3 * s, GTPlayer.Instance.locomotionEnabledLayers))
                ground = hit.point;
            Vector3 groundUnderShoulder = shoulder + Vector3.Project(ground - shoulder, up);

            Vector3 position;
            if (paddlePhase < .6f)
            {
                // Stroke: on (slightly into) the ground, ahead to behind
                float t = paddlePhase / .6f;
                position = groundUnderShoulder + facing * Mathf.Lerp(PaddleReach, -PaddleReach, t) * s - up * PaddlePress * s;
            }
            else
            {
                // Recovery: lift and swing forward
                float t = (paddlePhase - .6f) / .4f;
                position = Vector3.Lerp(groundUnderShoulder - facing * PaddleReach * s, groundUnderShoulder + facing * PaddleReach * s, t)
                    + up * Mathf.Sin(t * Mathf.PI) * .25f * s;
            }
            hand.Place(position, up, facing);
        }

        void LogDiagnostics(HandDriver holding, HoverboardVisual visual)
        {
            if (Time.time < nextLog) return;
            nextLog = Time.time + LogInterval;
            var player = GTPlayer.Instance;
            Transform board = visual.transform;
            Vector3 facing = Gravity.Flatten(body.forward).normalized;
            float pointingError = Vector3.Angle(Gravity.Flatten(board.forward), facing);
            float tilt = Vector3.Angle(board.up, Gravity.Up);
            bool paddleTouching = false;
            try
            {
                var free = visual.IsLeftHanded ? player.RightHand : player.LeftHand;
                paddleTouching = Traverse.Create(free).Field("isColliding").GetValue<bool>();
            }
            catch { }
            var traverse = Traverse.Create(player);
            // How high the board is over the ground, next to the height the game hovers it at
            string hoverHeight = "no ground below";
            if (Physics.Raycast(board.position + Gravity.Up * .5f, Gravity.Down, out RaycastHit ground, 5, player.locomotionEnabledLayers))
                hoverHeight = $"{ground.distance - .5f:0.00}m (ideal {traverse.Field("hoverIdealHeight").GetValue<float>():0.00}m)";
            Logging.Info($"Hoverboard: speed {rigidbody.linearVelocity.magnitude:0.0} m/s, " +
                $"board height {hoverHeight}, W held {InputHandler.inputDirection.z > 0}, " +
                $"hovering {traverse.Field("didHoverLastFrame").GetValue<bool>()}, " +
                $"hover point {traverse.Field("hasHoverPoint").GetValue<bool>()}, " +
                $"allowed {player.isHoverAllowed}, board points {pointingError:0}° off facing, " +
                $"board tilt {tilt:0}°, board {(board.position - body.position).magnitude:0.00}m from body, " +
                $"paddle touching {paddleTouching}");
        }
    }
}
