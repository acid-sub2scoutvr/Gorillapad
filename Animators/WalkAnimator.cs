using GorillaLocomotion;
using UnityEngine;
using UnityEngine.InputSystem;
using Gorillapad.Rigging;
using Gorillapad.Tools;

namespace Gorillapad.Animators
{
    /// <summary>
    /// Procedural knuckle-walk.
    /// - The gait cycle advances with the distance the body actually travels, so cadence follows
    ///   speed and the hands stop stepping if you're pushing against a wall.
    /// - A planted hand stays exactly where it landed while the body passes over it (no sliding).
    /// - A swinging hand aims ahead of the body so that it ends up as far behind as it started
    ///   ahead; the landing spot is re-raycast every tick so steps follow turns and slopes.
    /// - Standing still, hands settle at rest spots and take corrective steps when you turn.
    /// - The body bobs once per step (lowest at each plant), sways toward the weight-bearing arm,
    ///   and dips when landing.
    /// All distances are in meters at scale 1 and get multiplied by the player's scale.
    /// </summary>
    public class WalkAnimator : AnimatorBase
    {
        // ---- Tuning ----
        // Body movement (same speeds as the classic walk)
        const float WalkSpeed = 4f, SprintMultiplier = 3f;
        const float RideHeight = .46f, CrouchDrop = .3f;
        const float WalkBob = .16f, SprintBob = .22f, IdleBob = .03f;
        const float Sway = .04f;
        const float LandingDip = .12f;

        // Gait: stride is how far the body travels per full cycle (each hand steps once);
        // duty is the fraction of the cycle each hand spends planted
        const float WalkStride = 1f, SprintStride = 2f;
        const float WalkDuty = .65f, SprintDuty = .45f;
        const float WalkStepHeight = .12f, SprintStepHeight = .22f;
        const float SwingCurlDegrees = 25f;     // wrist flick during the swing
        const float MinGaitSpeed = .1f;         // m/s; slower than this counts as standing
        const float IdleStepThreshold = .18f;   // standing hand this far from its rest spot steps
        const float IdleStepDuration = .18f;    // seconds per corrective step

        // Walls: anything steeper than this (dot of normal with up) counts as a wall
        const float WallMaxUpDot = .7f, WallMinUpDot = -.3f;   // ~45 degrees .. slight overhang
        const float WallReach = .7f;            // how far from the body a wall can be jumped off
        const float WallJumpUpFactor = .9f;     // upward speed, relative to a normal jump
        const float WallJumpPush = 2f;          // m/s away from the wall
        const float WallJumpCooldown = .2f;     // seconds between wall jumps
        const float WallSlapDuration = .15f;    // how long a hand stays on the wall after a jump
        const float AirAcceleration = 8f;       // m/s^2 of steering in the air

        class Limb
        {
            public HandDriver driver;
            public float phaseOffset;           // 0 for left, .5 for right
            public bool planted;
            public float swingT;                // 0..1 progress through the current swing
            public Vector3 liftPoint;           // where the current swing started
            public Vector3 plantPoint, plantNormal = Vector3.up;
            public Vector3 lastTravelDirection;  // aims the swing; zero while standing
            public float wallHoldUntil;         // time a wall slap ends
        }

        Limb left, right;
        float phase;                            // 0..1 position in the gait cycle
        float moveWeight;                       // 0 standing .. 1 walking, smoothed
        float height = RideHeight, dip;
        bool wasMoving, wasOnGround = true, needsPlant = true;
        float Scale => GTPlayer.Instance.scale;
        static Gamepad CurrentGamepad => Gamepad.current;
        Vector2 LeftStickInput => CurrentGamepad != null ? Vector2.ClampMagnitude(CurrentGamepad.leftStick.ReadValue(), 1f) : Vector2.zero;
        bool IsSprinting => CurrentGamepad != null && CurrentGamepad.leftStickButton.isPressed;
        bool IsCrouching => CurrentGamepad != null && CurrentGamepad.buttonEast.isPressed;
        bool NotMoving => LeftStickInput.sqrMagnitude < 0.01f;
        float Stride => (IsSprinting ? SprintStride : WalkStride) * Scale;
        float Duty => IsSprinting ? SprintDuty : WalkDuty;
        float GroundSpeed => (IsSprinting ? WalkSpeed * SprintMultiplier : WalkSpeed) * 0.4f * Scale;
        float JumpSpeed => 250 * Scale / rigidbody.mass;
        Vector3 MoveInput => new Vector3(LeftStickInput.x, 0f, LeftStickInput.y);
        Vector3 InputDirection => Gravity.Flatten(body.TransformDirection(MoveInput)).normalized;

        protected override void Start()
        {
            base.Start();
            left = new Limb() { driver = leftHand, phaseOffset = 0 };
            right = new Limb() { driver = rightHand, phaseOffset = .5f };
        }

        public override void Setup()
        {
            HeadDriver.Instance.LockCursor = true;
            HeadDriver.Instance.turn = true;
            // Plant both hands fresh on the next tick
            needsPlant = true;
        }

        public override void Cleanup()
        {
            base.Cleanup();
            leftHand.followRate = rightHand.followRate = .1f;
        }

        public override void Animate()
        {
            if (rig == null || left == null) return; // Start hasn't run yet
            MoveBody();
            AnimateHands();
        }

        #region Jumping
        bool hasJumped, onJumpCooldown;
        float jumpTime, lastWallJump;
        Limb lastSlapLimb;
        void Update()
        {
            if (!Plugin.Instance.Enabled || rig == null || left == null) return;

            if (CurrentGamepad != null && CurrentGamepad.buttonSouth.wasPressedThisFrame)
            {
                // Holding a direction into a wall jumps off it (on the ground or in the air)
                if (Time.time - lastWallJump > WallJumpCooldown && FindWall(out RaycastHit wall))
                    WallJump(wall);
                else if (!hasJumped && rig.onGround)
                {
                    StartJump();
                    rigidbody.AddForce(Gravity.Up * 250 * Scale, ForceMode.Impulse);
                }
            }
            if (hasJumped && !rig.onGround || Time.time - jumpTime > 1)
                onJumpCooldown = false;
            if (rig.onGround && !onJumpCooldown)
                hasJumped = false;
        }

        void StartJump()
        {
            hasJumped = true;
            onJumpCooldown = true;
            jumpTime = Time.time;
            rig.active = false;
        }

        // A steep surface in the direction you're holding, within reach of the body
        bool FindWall(out RaycastHit wall)
        {
            wall = default;
            Vector3 direction = InputDirection;
            if (direction == Vector3.zero) return false;
            float s = Scale;
            if (!Physics.SphereCast(body.position, .15f * s, direction, out wall, WallReach * s,
                GTPlayer.Instance.locomotionEnabledLayers))
                return false;
            float upDot = Vector3.Dot(wall.normal, Gravity.Up);
            return upDot < WallMaxUpDot && upDot > WallMinUpDot;
        }

        void WallJump(RaycastHit wall)
        {
            StartJump();
            lastWallJump = Time.time;

            // Keep half of any speed along the wall, then go up and push off
            Vector3 away = Vector3.ProjectOnPlane(wall.normal, Gravity.Up).normalized;
            Vector3 along = Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(rigidbody.linearVelocity, wall.normal), Gravity.Up);
            rigidbody.linearVelocity = along * .5f + Gravity.Up * JumpSpeed * WallJumpUpFactor + away * WallJumpPush * Scale;

            // Slap the hand on the wall's side; alternate for walls straight ahead
            float side = Vector3.Dot(-away, body.right);
            Limb limb = Mathf.Abs(side) > .3f ? (side > 0 ? right : left) : (lastSlapLimb == left ? right : left);
            lastSlapLimb = limb;
            limb.planted = false;
            limb.plantPoint = wall.point + wall.normal * .03f * Scale;
            limb.plantNormal = wall.normal;
            limb.wallHoldUntil = Time.time + WallSlapDuration;
        }
        #endregion

        #region Body
        void MoveBody()
        {
            rig.active = rig.onGround && !hasJumped;
            rig.useGravity = !rig.onGround;
            // The rig counts as grounded with ground anywhere ~1.3m below, so also steer right
            // after a jump (e.g. the first bounce off a wall)
            if (!rig.onGround || hasJumped)
                AirControl();
            if (!rig.onGround)
                return;

            // No stepping bob while sliding
            moveWeight = Mathf.Lerp(moveWeight, NotMoving || rig.sliding ? 0 : 1, Extensions.Smoothing(.15f));

            // Gait bob: two bumps per cycle (one per step). Each hand plants at phase = its offset,
            // so cos(4*pi*phase) puts the low points at the plants and the high points mid-stance.
            float gaitBob = (IsSprinting ? SprintBob : WalkBob) * (.5f - .5f * Mathf.Cos(phase * Mathf.PI * 4));
            float idleBob = IdleBob * (.5f + .5f * Mathf.Sin(Time.time * Mathf.PI * 2));
            float targetHeight = RideHeight + Mathf.Lerp(idleBob, gaitBob, moveWeight);
            if (IsCrouching)
                targetHeight -= CrouchDrop;

            // Dip on landing, then recover
            if (!wasOnGround)
                dip = LandingDip;
            dip = Mathf.Lerp(dip, 0, Extensions.Smoothing(.08f));
            targetHeight -= dip;

            height = Mathf.Lerp(height, targetHeight, Extensions.Smoothing(.3f));
            Vector3 targetPosition = rig.lastGroundPosition + Gravity.Up * height * Scale;

            // Sway toward whichever hand is bearing weight (left mid-stance is at phase = duty/2)
            float swayAmount = Mathf.Cos((phase - Duty / 2) * Mathf.PI * 2) * Sway * Scale * moveWeight;
            targetPosition -= body.right * swayAmount;

            // Movement
            Vector3 direction = Gravity.Flatten(body.TransformDirection(MoveInput));
            if (Vector3.Dot(rig.lastNormal, Gravity.Up) > .3f) // If the slope is too steep, don't correct the direction
                direction = Vector3.ProjectOnPlane(direction, rig.lastNormal);
            float speed = IsSprinting ? WalkSpeed * SprintMultiplier : WalkSpeed;
            targetPosition += direction * Scale * speed / 10f;
            rig.targetPosition = targetPosition;
        }

        // Steer toward the held direction in the air (this is what brings you back to a wall
        // between bounces). Holding a direction can turn you or slow you down, and speeds you up
        // to walking speed, but never cuts momentum you already have (off a ramp, zipline, rope...).
        void AirControl()
        {
            Vector3 direction = InputDirection;
            if (direction == Vector3.zero) return;
            Vector3 horizontal = Vector3.ProjectOnPlane(rigidbody.linearVelocity, Gravity.Up);
            Vector3 next = horizontal + direction * AirAcceleration * Scale * Time.deltaTime;
            next = Vector3.ClampMagnitude(next, Mathf.Max(horizontal.magnitude, GroundSpeed));
            rigidbody.AddForce(next - horizontal, ForceMode.VelocityChange);
        }
        #endregion

        #region Hands
        void AnimateHands()
        {
            if (!rig.onGround || hasJumped)
            {
                AirPose();
                wasOnGround = false;
                wasMoving = false;
                return;
            }
            if (!wasOnGround || needsPlant)
            {
                // Just landed (or just selected): put both hands down under the shoulders
                PlantAt(left, RestSpot(left));
                PlantAt(right, RestSpot(right));
                wasOnGround = true;
                needsPlant = false;
                wasMoving = false;
            }

            if (rig.sliding)
            {
                SlidePose();
                wasMoving = false;
                return;
            }

            float dt = Time.deltaTime;
            Vector3 velocity = Vector3.ProjectOnPlane(rigidbody.linearVelocity, Gravity.Up);
            float speed = velocity.magnitude;
            bool moving = speed > MinGaitSpeed * Scale && !NotMoving;

            if (moving)
            {
                if (!wasMoving)
                    SyncPhaseToHands();
                Vector3 travelDirection = velocity / speed;
                phase = Mathf.Repeat(phase + speed * dt / Stride, 1);

                UpdateMovingLimb(left, travelDirection);
                UpdateMovingLimb(right, travelDirection);

                // If the body has run away from a planted hand (sharp turn, knocked around),
                // lift it now and re-sync the cycle around it. Normally a planted hand gets at most
                // half a stance (stride * duty / 2) from its shoulder before lifting.
                float reachLimit = Mathf.Max(.3f * Scale, Stride * Duty * .8f);
                foreach (var limb in new[] { left, right })
                {
                    var other = limb == left ? right : left;
                    if (limb.planted && other.planted && FlatDistance(limb.plantPoint, RestSpot(limb).point) > reachLimit)
                    {
                        phase = Mathf.Repeat(Duty - limb.phaseOffset + .001f, 1);
                        BeginSwing(limb);
                        break;
                    }
                }
            }
            else
            {
                UpdateStandingLimb(left, right, dt);
                UpdateStandingLimb(right, left, dt);
            }
            wasMoving = moving;

            Pose(left);
            Pose(right);
        }

        // Phase-driven stepping while moving
        void UpdateMovingLimb(Limb limb, Vector3 travelDirection)
        {
            float p = Mathf.Repeat(phase + limb.phaseOffset, 1);
            if (p >= Duty)
            {
                if (limb.planted)
                    BeginSwing(limb);
                limb.swingT = (p - Duty) / (1 - Duty);
                limb.lastTravelDirection = travelDirection;
            }
            else if (!limb.planted)
                PlantAt(limb, Target(limb, travelDirection, 1));
        }

        // Time-driven stepping while standing: finish any swing, then take corrective steps
        void UpdateStandingLimb(Limb limb, Limb other, float dt)
        {
            limb.lastTravelDirection = Vector3.zero;
            if (!limb.planted)
            {
                limb.swingT += dt / IdleStepDuration;
                if (limb.swingT >= 1)
                    PlantAt(limb, RestSpot(limb));
                return;
            }
            // Only one hand steps at a time, and the one that's furthest off goes first
            if (!other.planted) return;
            float error = limb.plantPoint.Distance(RestSpot(limb).point);
            float otherError = other.plantPoint.Distance(RestSpot(other).point);
            if (error > IdleStepThreshold * Scale && error >= otherError)
                BeginSwing(limb);
        }

        // When starting to move, line the cycle up with whichever hand is already mid-swing,
        // otherwise start the left hand's swing right away
        void SyncPhaseToHands()
        {
            foreach (var limb in new[] { left, right })
            {
                if (!limb.planted)
                {
                    phase = Mathf.Repeat(Duty + limb.swingT * (1 - Duty) - limb.phaseOffset, 1);
                    return;
                }
            }
            phase = Mathf.Repeat(Duty - left.phaseOffset + .001f, 1);
        }

        void BeginSwing(Limb limb)
        {
            limb.planted = false;
            limb.swingT = 0;
            limb.liftPoint = limb.plantPoint;
        }

        void PlantAt(Limb limb, GroundHit spot)
        {
            limb.planted = true;
            limb.swingT = 0;
            limb.plantPoint = spot.point;
            limb.plantNormal = spot.normal;
        }

        static float FlatDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Gravity.Up).magnitude;

        // Where a swinging hand should land. It leads the body by enough that it plants ahead of
        // the shoulder and gets passed by half a stance's worth of travel before lifting again.
        GroundHit Target(Limb limb, Vector3 travelDirection, float swingT)
        {
            float lead = Stride * ((1 - swingT) * (1 - Duty) + Duty / 2);
            return GroundBelow(limb.driver.DefaultPosition + travelDirection * lead, limb);
        }

        GroundHit RestSpot(Limb limb) => GroundBelow(limb.driver.DefaultPosition, limb);

        struct GroundHit { public Vector3 point, normal; }

        GroundHit GroundBelow(Vector3 point, Limb limb)
        {
            float s = Scale;
            if (Physics.Raycast(point + Gravity.Up * .4f * s, Gravity.Down, out RaycastHit hit,
                .9f * s, GTPlayer.Instance.locomotionEnabledLayers))
                return new GroundHit() { point = hit.point, normal = hit.normal };
            // Nothing below (ledge): let the hand hang where it would be
            return new GroundHit() { point = point, normal = limb.planted ? limb.plantNormal : Gravity.Up };
        }

        // Positions and orients the hand
        void Pose(Limb limb)
        {
            var hand = limb.driver;
            hand.followInBodySpace = false;
            hand.hideControllerTransform = true;
            Vector3 position, normal;
            float curl = 0;
            if (limb.planted)
            {
                position = limb.plantPoint;
                normal = limb.plantNormal;
                // Pinned where it landed; a touch of smoothing so plants and landings don't pop
                hand.followRate = .7f;
            }
            else
            {
                // Standing steps (no travel direction) go to the rest spot
                var target = limb.lastTravelDirection == Vector3.zero ?
                    RestSpot(limb) :
                    Target(limb, limb.lastTravelDirection, limb.swingT);
                float t = Mathf.SmoothStep(0, 1, limb.swingT);
                float arc = Mathf.Sin(limb.swingT * Mathf.PI);
                float stepHeight = (IsSprinting ? SprintStepHeight : WalkStepHeight) * Scale;
                normal = Vector3.Slerp(limb.plantNormal, target.normal, t);
                position = Vector3.Lerp(limb.liftPoint, target.point, t) + normal * stepHeight * arc;
                curl = SwingCurlDegrees * arc;
                hand.followRate = .6f;
            }
            // Knuckles forward along the ground, tilted with slopes
            Orient(limb, position, normal, body.forward, curl);
        }

        void Orient(Limb limb, Vector3 position, Vector3 normal, Vector3 forwardHint, float curl) =>
            limb.driver.Place(position, normal, forwardHint, curl);

        // Sliding: no stepping; hands rest on the ground beside you and slide along with you,
        // a little wider and further forward for balance. They stay planted there for when the
        // slide ends.
        void SlidePose()
        {
            float s = Scale;
            foreach (var limb in new[] { left, right })
            {
                var hand = limb.driver;
                float side = limb == left ? -1 : 1;
                var spot = GroundBelow(hand.DefaultPosition + (body.right * side * .08f + body.forward * .05f) * s, limb);
                PlantAt(limb, spot);
                hand.followInBodySpace = true;
                hand.followRate = .4f;
                Vector3 direction = rig.slideVelocity.sqrMagnitude > .01f ? rig.slideVelocity : body.forward;
                Orient(limb, spot.point, spot.normal, direction, 0);
            }
        }

        // In the air: a hand that just slapped a wall stays on it briefly; near a wall you're holding
        // toward, hands reach for it; otherwise hands up and a little forward like the classic walk
        void AirPose()
        {
            float s = Scale;
            bool nearWall = FindWall(out RaycastHit wall);
            // Arms rise a bit the faster you fall
            float fallRaise = Mathf.Clamp(-Gravity.Height(rigidbody.linearVelocity) / Scale * .03f, 0, .25f);
            // Horizontal direction along the wall, pointing to the body's right
            Vector3 alongWall = nearWall ? Vector3.Cross(Gravity.Up, wall.normal).normalized : Vector3.zero;
            if (Vector3.Dot(alongWall, body.right) < 0)
                alongWall = -alongWall;

            foreach (var limb in new[] { left, right })
            {
                var hand = limb.driver;
                limb.planted = false;
                limb.swingT = 0;
                limb.liftPoint = hand.transform.position;

                // Let go early if the body has already flown too far from the slap
                if (Time.time < limb.wallHoldUntil && limb.plantPoint.Distance(hand.DefaultPosition) < .8f * s)
                {
                    hand.followInBodySpace = false;
                    hand.followRate = .7f;
                    Orient(limb, limb.plantPoint, limb.plantNormal, Gravity.Up, 0);
                    continue;
                }
                limb.wallHoldUntil = 0;

                if (nearWall && alongWall != Vector3.zero)
                {
                    Vector3 side = alongWall * (limb == left ? -.22f : .22f);
                    Vector3 point = wall.point + (side + Gravity.Up * .2f + wall.normal * .08f) * s;
                    hand.followInBodySpace = false;
                    hand.followRate = .25f;
                    Orient(limb, point, wall.normal, Gravity.Up, 0);
                    continue;
                }

                // Hang off the body (smoothed relative to it) so the arms don't trail behind a fast fall
                hand.followInBodySpace = true;
                hand.followRate = .15f;
                hand.targetPosition = hand.DefaultPosition + (Gravity.Up * (.15f + fallRaise) + body.forward * .1f) * s;
                hand.lookAt = hand.targetPosition + body.forward;
                hand.up = limb == left ? body.right : -body.right;
            }
        }
        #endregion
    }
}
