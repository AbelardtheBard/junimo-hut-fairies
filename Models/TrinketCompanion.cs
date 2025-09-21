using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmCompanionRoamerMod.Models
{
public class TrinketCompanion
{
    // Unique identifier for incremental fairy management
    public string FairyBoxGUID { get; set; } = string.Empty;
    
    // Light source for fairy glow (emulating glow ring behavior)
    private LightSource? FairyLight { get; set; } = null;
    private string? lightSourceId = null;
    
    private int animationFrame = 0;
    private float animationTimer = 0f;
    private const float AnimationInterval = 0.12f; // seconds per frame
        private Vector2 targetDirection = Vector2.Zero;
        private float directionLerp = 1f;
        private Vector2 velocity = Vector2.Zero;
        private bool facingLeft = false;
        private float facingFlipCooldown = 0f;
        // For smoothed facing logic
        private float smoothedMoveX = 0f;
        private float facingFlipDebounce = 0f;
        public Vector2 Position { get; set; }
        public int StyleIndex { get; set; }
        public string TrinketType { get; set; }

        // Movement state
        private Vector2 homeTile; // Hut center in tile coordinates
        private Vector2 targetTile; // Target tile to float toward
        private float speed; // Tiles per second
        private float bobPhase; // For up/down bobbing
        private float bobAmplitude; // Bobbing height in pixels
        private float flutterPhase; // For fluttering
        private float flutterAmplitude; // Fluttering offset
        private float idleTimer; // Idle pause timer
        private float changeTargetTimer; // Time until next target change
        private static readonly Random rng = new Random();

        public TrinketCompanion(Vector2 homeTile, int styleIndex, string trinketType = "Fairy")
        {
            this.homeTile = homeTile;
            this.Position = homeTile; // Start at hut center
            this.StyleIndex = styleIndex;
            this.TrinketType = trinketType;
            PickNewTarget();
            speed = 0.25f + (float)rng.NextDouble() * 0.25f; // 0.25-0.5 tiles/sec (even slower)
            bobPhase = (float)rng.NextDouble() * MathF.PI * 2f;
            bobAmplitude = 8f + (float)rng.NextDouble() * 4f; // 8-12 px
            flutterPhase = (float)rng.NextDouble() * MathF.PI * 2f;
            flutterAmplitude = 2f + (float)rng.NextDouble() * 2f; // 2-4 px
            idleTimer = 0f;
            changeTargetTimer = 8.0f + (float)rng.NextDouble() * 2.0f; // 8-10s
            
            // Create fairy light source (always glowing like glow ring)
            CreateLightSource();
        }

        /// <summary>Create fairy light source following glow ring pattern</summary>
        public void CreateLightSource()
        {
            if (FairyLight != null) return;
            
            var farm = Game1.getLocationFromName("Farm") as Farm;
            if (farm == null) return;
            
            // Generate unique light ID for this fairy
            lightSourceId = $"fairy_{FairyBoxGUID}_{StyleIndex}_{Game1.random.Next(1000, 9999)}";
            
            // Create light at fairy sprite position (not ground position)
            Vector2 groundPos = Position * 64f + new Vector2(32f, 64f);
            Vector2 fairyPos = groundPos;
            fairyPos.Y += -200f + MathF.Sin(bobPhase) * bobAmplitude; // Match fairy sprite position
            fairyPos.X += MathF.Sin(flutterPhase) * flutterAmplitude;
            
            Color lightColor = GetFairyLightColor();
            float lightRadius = 3f; // Smaller than glow ring for subtlety
            
            FairyLight = new LightSource(lightSourceId, LightSource.lantern, fairyPos, lightRadius, lightColor, LightSource.LightContext.None, 0L);
            
            // Add to farm's shared lights (emulating ring behavior)
            farm.sharedLights[lightSourceId] = FairyLight;
        }
        
        /// <summary>Remove fairy light source</summary>
        public void RemoveLightSource()
        {
            if (lightSourceId == null) return;
            
            var farm = Game1.getLocationFromName("Farm") as Farm;
            if (farm != null)
            {
                farm.sharedLights.Remove(lightSourceId);
            }
            
            FairyLight = null;
            lightSourceId = null;
        }
        
        /// <summary>Update light position as fairy moves (emulating how glow ring follows player)</summary>
        public void UpdateLightPosition()
        {
            if (FairyLight != null)
            {
                // Match the exact positioning used in Draw() method for the fairy sprite
                Vector2 groundPos = Position * 64f + new Vector2(32f, 64f);
                Vector2 fairyPos = groundPos;
                fairyPos.Y += -200f + MathF.Sin(bobPhase) * bobAmplitude; // Same calculation as fairy sprite
                fairyPos.X += MathF.Sin(flutterPhase) * flutterAmplitude;
                
                // Position light at the fairy sprite location
                FairyLight.position.Value = fairyPos;
            }
        }
        
        /// <summary>Get consistent glow light color for all fairies (emulating glow ring)</summary>
        private Color GetFairyLightColor()
        {
            // Use same color as glow ring for consistency: soft blue glow
            return new Color(0, 30, 150, 255); // Same as glow ring "517"
        }

        // Pick a new target tile within 10 tiles of home in each direction
        private void PickNewTarget()
        {
            // Set up smooth transition to new direction
            float minRadius = 8f;
            float maxRadius = 10f;
            int attempts = 0;
            Vector2 candidate;
            do {
                float angle = (float)(rng.NextDouble() * MathF.PI * 2f);
                float radius = minRadius + (float)rng.NextDouble() * (maxRadius - minRadius);
                // Balance vertical movement: avoid bias toward down
                if (MathF.Sin(angle) < -0.2f) angle = -angle;
                Vector2 forward = velocity.Length() > 0.1f ? velocity : (Position - homeTile);
                if (forward.Length() > 0.1f) {
                    float forwardAngle = MathF.Atan2(forward.Y, forward.X);
                    angle = angle * 0.3f + forwardAngle * 0.7f;
                }
                float dx = MathF.Cos(angle) * radius;
                float dy = MathF.Sin(angle) * radius;
                candidate = homeTile + new Vector2(dx, dy);
                attempts++;
            } while (Vector2.Distance(candidate, Position) < minRadius && attempts < 10);
            targetTile = candidate;
            Vector2 toTarget = targetTile - Position;
            if (toTarget.Length() > 0.1f) {
                targetDirection = Vector2.Normalize(toTarget);
                directionLerp = 0f;
            }
        }

        // Call this every tick (60/sec)
        public void Update(float dt)
        {
            // Decrement facing flip cooldown
            if (facingFlipCooldown > 0f)
                facingFlipCooldown -= dt;
            // Idle pause
            if (idleTimer > 0f)
            {
                idleTimer -= dt;
                // Bobbing and flutter still animate while idle
                bobPhase += dt * 2.2f;
                flutterPhase += dt * 3.5f;
                if (bobPhase > MathF.PI * 2f) bobPhase -= MathF.PI * 2f;
                if (flutterPhase > MathF.PI * 2f) flutterPhase -= MathF.PI * 2f;
                return;
            }
            // Move toward target with gentle curve and repulsion from other fairies
            Vector2 toTarget = targetTile - Position;
            float dist = toTarget.Length();
            Vector2 moveVec = Vector2.Zero;
            if (dist > 0.1f)
            {
                // Add gentle curve to movement: spiral toward target
                if (targetDirection != Vector2.Zero)
                {
                    directionLerp += dt * 0.35f; // slower, more curved
                    directionLerp = MathF.Min(directionLerp, 1f);
                    float spiralAngle = MathF.Sin(directionLerp * MathF.PI) * 0.4f; // arc effect
                    Vector2 baseDir = Vector2.Lerp(velocity.Length() > 0 ? Vector2.Normalize(velocity) : targetDirection, targetDirection, directionLerp);
                    Vector2 curvedDir = Vector2.Transform(baseDir, Matrix.CreateRotationZ(spiralAngle));
                    velocity = curvedDir * speed;
                }
                else
                {
                    // Fallback: keep current velocity
                    if (velocity == Vector2.Zero)
                        velocity = (toTarget / dist) * speed;
                }
                // Reduce random flutter for more intention
                float flutterAngle = MathF.Sin(flutterPhase) * 0.03f;
                velocity = Vector2.Transform(velocity, Matrix.CreateRotationZ(flutterAngle));
                moveVec = velocity;

                // --- Facing direction logic: use only main movement vector (before repulsion) ---
                // --- Facing direction logic: smooth and debounce ---
                // Exponential moving average for X direction
                float smoothing = 0.85f; // higher = smoother, lower = more responsive
                smoothedMoveX = smoothedMoveX * smoothing + moveVec.X * (1f - smoothing);
                // If smoothed X crosses zero and stays for debounce time, flip
                bool shouldFaceLeft = smoothedMoveX > 0.1f;
                bool shouldFaceRight = smoothedMoveX < -0.1f;
                if ((shouldFaceLeft && !facingLeft) || (shouldFaceRight && facingLeft))
                {
                    facingFlipDebounce += dt;
                    if (facingFlipDebounce > 0.18f) // must persist for 0.18s
                    {
                        facingLeft = shouldFaceLeft;
                        facingFlipDebounce = 0f;
                    }
                }
                else
                {
                    facingFlipDebounce = 0f;
                }
            }
            // Strengthen gentle repulsion from other fairies (spread out, but allow brief overlap)
            if (FarmCompanionRoamerMod.ModEntry.hutCompanions != null)
            {
                foreach (var companions in FarmCompanionRoamerMod.ModEntry.hutCompanions.Values)
                {
                    foreach (var other in companions)
                    {
                        if (other == this) continue;
                        float repelDist = (other.Position - Position).Length();
                        if (repelDist < 2.0f && repelDist > 0.01f)
                        {
                            Vector2 away = (Position - other.Position) / repelDist;
                            float strength = (2.0f - repelDist) * 0.14f; // Stronger, but only if very close
                            moveVec += away * strength;
                        }
                    }
                }
            }
            if (moveVec != Vector2.Zero)
            {
                moveVec.Normalize();
                float move = MathF.Min(speed * dt, dist);
                Position += moveVec * move;
            }
            // If close, pick new target and maybe idle
            changeTargetTimer -= dt;
            if (dist < 0.2f || changeTargetTimer <= 0f)
            {
                PickNewTarget();
                changeTargetTimer = 8.0f + (float)rng.NextDouble() * 2.0f; // 8-10s
                // Removed idle after arrival to ensure fluid movement
            }
            // Bobbing and flutter
            bobPhase += dt * 2.2f; // Bob speed
            flutterPhase += dt * 3.5f; // Flutter speed
            if (bobPhase > MathF.PI * 2f) bobPhase -= MathF.PI * 2f;
            if (flutterPhase > MathF.PI * 2f) flutterPhase -= MathF.PI * 2f;
            // If near edge of allowed area, gently curve back
            Vector2 fromHome = Position - homeTile;
            if (fromHome.Length() > 10f)
            {
                Vector2 inward = homeTile - Position;
                Position += inward * 0.02f;
            }
            
            // Update fairy light position to follow fairy movement (like glow ring follows player)
            UpdateLightPosition();
        }

        public void Draw(SpriteBatch b)
        {
            // Calculate ground/tile position (center bottom of tile)
            Vector2 groundPos = Position * 64f + new Vector2(32f, 64f);

            // Draw shadow at ground position and layer depth (vanilla style)
            Texture2D shadowTex = StardewValley.Game1.shadowTexture ?? StardewValley.Game1.staminaRect;
            Vector2 shadowOrigin = new Vector2(shadowTex.Bounds.Center.X, shadowTex.Bounds.Center.Y);
            float shadowScale = 3f;
            Color shadowColor = Color.White;
            float groundLayerDepth = (groundPos.Y - 8f) / 10000f - 2E-06f;
            b.Draw(
                shadowTex,
                StardewValley.Game1.GlobalToLocal(StardewValley.Game1.viewport, groundPos),
                shadowTex.Bounds,
                shadowColor,
                0f,
                shadowOrigin,
                shadowScale,
                SpriteEffects.None,
                groundLayerDepth
            );

            // Calculate fairy's visual position (flying higher above shadow)
            Vector2 fairyPos = groundPos;
            fairyPos.Y += -200f + MathF.Sin(bobPhase) * bobAmplitude; // hover extremely high above shadow
            fairyPos.X += MathF.Sin(flutterPhase) * flutterAmplitude;

            // Draw fairy at same layer depth as shadow (so both are occluded together)
            float fairyLayerDepth = groundLayerDepth + 1E-06f;
            // Animate fairy wings: 4 frames horizontally, 16x16 each, starting at X=64
            animationTimer += (float)Game1.currentGameTime.ElapsedGameTime.TotalSeconds;
            if (animationTimer >= AnimationInterval)
            {
                animationFrame = (animationFrame + 1) % 4;
                animationTimer -= AnimationInterval;
            }
            // 1-based config: FairyStyleID=1 shows first sprite, 2=second, ..., 8=last
            int style = Math.Max(1, Math.Min(StyleIndex, 8)) - 1;
            int styleGroup = style / 4; // 0 for top row, 1 for bottom row
            int styleOffset = style % 4; // 0-3, column in row
            // Each fairy has 4 animation frames horizontally, starting at X=64
            int frameX = (styleOffset * 4 + animationFrame) * 16;
            int frameY = styleGroup == 0 ? 0 : 176;
            var src = new Rectangle(frameX, frameY, 16, 16);
            Vector2 fairyOrigin = new Vector2(src.Width / 2f, src.Height / 2f);
            b.Draw(
                StardewValley.Game1.content.Load<Texture2D>("TileSheets/companions"),
                StardewValley.Game1.GlobalToLocal(StardewValley.Game1.viewport, fairyPos),
                src,
                Color.White,
                0f,
                fairyOrigin,
                4f,
                facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                fairyLayerDepth
            );
        }
    }
}
