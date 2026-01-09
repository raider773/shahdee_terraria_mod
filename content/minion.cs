using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;

namespace shahdee_mod.content
{
	// This minion shows a few mandatory things that make it behave properly.
	// Its attack pattern is simple: If an enemy is in range of 43 tiles, it will fly to it and deal contact damage
	// If the player targets a certain NPC with right-click, it will fly through tiles to it
	// If it isn't attacking, it will float near the player with minimal movement
	public class minion : ModProjectile
	{

		public override string Texture =>
            "shahdee_mod/assets/minion";




		private enum AnimState {
				Idle,
				Walk,
				Jump
			}

			private AnimState animState;
	private AnimState previousAnimState;


		public override void SetStaticDefaults() {
			// Sets the amount of frames this minion has on its spritesheet
			Main.projFrames[Type] = 14;
			// This is necessary for right-click targeting
			ProjectileID.Sets.MinionTargettingFeature[Type] = true;

			Main.projPet[Type] = true; // Denotes that this projectile is a pet or minion

			ProjectileID.Sets.MinionSacrificable[Type] = true; // This is needed so your minion can properly spawn when summoned and replaced when other minions are summoned
			ProjectileID.Sets.CultistIsResistantTo[Type] = true; // Make the cultist resistant to this projectile, as it's resistant to all homing projectiles.
		}

		public sealed override void SetDefaults() {
			Projectile.width = 18;
			Projectile.height = 28;
			Projectile.tileCollide = true; // Makes the minion go through tiles freely

			// These below are needed for a minion weapon
			Projectile.friendly = true; // Only controls if it deals damage to enemies on contact (more on that later)
			Projectile.minion = true; // Declares this as a minion (has many effects)
			Projectile.DamageType = DamageClass.Melee; // Declares the damage type (needed for it to deal damage)
			Projectile.minionSlots = 1f; // Amount of slots this minion occupies from the total minion slots available to the player (more on that later)
			Projectile.penetrate = -1; // Needed so the minion doesn't despawn on collision with enemies or tiles

		}

		public override bool PreDraw(ref Color lightColor)
			{
				// Get the texture
				Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

				// Calculate the sprite's position on screen
				Vector2 drawPosition = Projectile.Center - Main.screenPosition;

				// Shift it up so it sits on the ground correctly
				drawPosition.Y += -10; // tweak this number until it looks correct

				// Determine the frame to draw
				Rectangle frame = texture.Frame(1, Main.projFrames[Projectile.type], 0, Projectile.frame);

				// Draw the sprite
				Main.spriteBatch.Draw(
					texture,
					drawPosition,
					frame,
					lightColor,
					Projectile.rotation,
					frame.Size() / 2,  // origin is center of frame
					1f,                // scale
					Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally,
					0f
				);

				return false; // skip default drawing
			}		

		// Here you can decide if your minion breaks things like grass or pots
		public override bool? CanCutTiles() {
			return false;
		}

		// This is mandatory if your minion deals contact damage (further related stuff in AI() in the Movement region)
		public override bool MinionContactDamage() {
			return false;
		}

		// The AI of this minion is split into multiple methods to avoid bloat. This method just passes values between calls actual parts of the AI.
		public override void AI() {
								previousAnimState = animState;
				Player owner = Main.player[Projectile.owner];

				if (!CheckActive(owner))
					return;

				GeneralBehavior(owner, out Vector2 vectorToIdlePosition, out float distanceToIdlePosition);
				bool foundTarget = false;
				float distanceFromTarget = 0f;
				Vector2 targetCenter = Vector2.Zero;

				Movement(foundTarget, distanceFromTarget, targetCenter, distanceToIdlePosition, vectorToIdlePosition);

				// === ANIMATION STATE DECISION ===
				bool onGround = false;
				int tileXLeft = (int)(Projectile.position.X / 16f);
				int tileXRight = (int)((Projectile.position.X + Projectile.width) / 16f);
				int tileY = (int)((Projectile.position.Y + Projectile.height + 1) / 16f);
				for (int x = tileXLeft; x <= tileXRight; x++) {
					Tile tile = Main.tile[x, tileY];
					if (tile != null && tile.HasTile) {
						bool solidBlock = Main.tileSolid[tile.TileType];
						bool platform = Main.tileSolidTop[tile.TileType] && tile.TileFrameY == 0;
						if (solidBlock || platform) {
							onGround = true;
							break;
						}
					}
				}

								if (!onGround) {
									animState = AnimState.Jump;
								} else if (Math.Abs(Projectile.velocity.X) < 0.2f) {
									animState = AnimState.Idle;
									Projectile.velocity.X = 0f;
								} else {
									animState = AnimState.Walk;
								}

				Visuals();
			}


		// This is the "active check", makes sure the minion is alive while the player is alive, and despawns if not
		private bool CheckActive(Player owner) {
			if (owner.dead || !owner.active) {
				owner.ClearBuff(ModContent.BuffType<buff>());

				return false;
			}

			if (owner.HasBuff(ModContent.BuffType<buff>())) {
				Projectile.timeLeft = 2;
			}

			return true;
		}

		private void GeneralBehavior(Player owner, out Vector2 vectorToIdlePosition, out float distanceToIdlePosition) {
			Vector2 idlePosition = owner.Center;
			idlePosition.Y -= 0f; // Go up 48 coordinates (three tiles from the center of the player)

			// If your minion doesn't aimlessly move around when it's idle, you need to "put" it into the line of other summoned minions
			// The index is projectile.minionPos
			float minionPositionOffsetX = (6 + Projectile.minionPos * 30) * -owner.direction;
			idlePosition.X += minionPositionOffsetX; // Go behind the player

			// All of this code below this line is adapted from Spazmamini code (ID 388, aiStyle 66)

			// Teleport to player if distance is too big
			vectorToIdlePosition = idlePosition - Projectile.Center;
			distanceToIdlePosition = vectorToIdlePosition.Length();

			if (Main.myPlayer == owner.whoAmI && distanceToIdlePosition > 2000f) {
				// Whenever you deal with non-regular events that change the behavior or position drastically, make sure to only run the code on the owner of the projectile,
				// and then set netUpdate to true
				Projectile.position = idlePosition;
				Projectile.velocity *= 0.1f;
				Projectile.netUpdate = true;
			}

			// If your minion is flying, you want to do this independently of any conditions
			float overlapVelocity = 0.04f;

			// Fix overlap with other minions
			foreach (var other in Main.ActiveProjectiles) {
				if (other.whoAmI != Projectile.whoAmI && other.owner == Projectile.owner && Math.Abs(Projectile.position.X - other.position.X) + Math.Abs(Projectile.position.Y - other.position.Y) < Projectile.width) {
					if (Projectile.position.X < other.position.X) {
						Projectile.velocity.X -= overlapVelocity;
					}
					else {
						Projectile.velocity.X += overlapVelocity;
					}

					if (Projectile.position.Y < other.position.Y) {
						Projectile.velocity.Y -= overlapVelocity;
					}
					else {
						Projectile.velocity.Y += overlapVelocity;
					}
				}
			}
		}

		private void SearchForTargets(Player owner, out bool foundTarget, out float distanceFromTarget, out Vector2 targetCenter) {
			// Starting search distance
			distanceFromTarget = 120f;
			targetCenter = Projectile.position;
			foundTarget = false;

			// This code is required if your minion weapon has the targeting feature
			if (owner.HasMinionAttackTargetNPC) {
				NPC npc = Main.npc[owner.MinionAttackTargetNPC];
				float between = Vector2.Distance(npc.Center, Projectile.Center);

				// Reasonable distance away so it doesn't target across multiple screens
				if (between < 130f) {
					distanceFromTarget = between;
					targetCenter = npc.Center;
					foundTarget = true;
				}
			}

			if (!foundTarget) {
				// This code is required either way, used for finding a target
				foreach (var npc in Main.ActiveNPCs) {
					if (npc.CanBeChasedBy()) {
						float between = Vector2.Distance(npc.Center, Projectile.Center);
						bool closest = Vector2.Distance(Projectile.Center, targetCenter) > between;
						bool inRange = between < distanceFromTarget;
						bool lineOfSight = Collision.CanHitLine(Projectile.position, Projectile.width, Projectile.height, npc.position, npc.width, npc.height);
						// Additional check for this specific minion behavior, otherwise it will stop attacking once it dashed through an enemy while flying though tiles afterwards
						// The number depends on various parameters seen in the movement code below. Test different ones out until it works alright
						bool closeThroughWall = between < 100f;

						if (((closest && inRange) || !foundTarget) && (lineOfSight || closeThroughWall)) {
							distanceFromTarget = between;
							targetCenter = npc.Center;
							foundTarget = true;
						}
					}
				}
			}

			// friendly needs to be set to true so the minion can deal contact damage
			// friendly needs to be set to false so it doesn't damage things like target dummies while idling
			// Both things depend on if it has a target or not, so it's just one assignment here
			// You don't need this assignment if your minion is shooting things instead of dealing contact damage
			Projectile.friendly = false;
		}

		private void Movement(bool foundTarget, float distanceFromTarget, Vector2 targetCenter, float distanceToIdlePosition, Vector2 vectorToIdlePosition) {
				float walkSpeed = 5f;
				float acceleration = 0.15f;
				float normalJump = -8f;
				float highJump = -8f;

				// gravity
				Projectile.velocity.Y += 0.3f;
				if (Projectile.velocity.Y > 8f)
					Projectile.velocity.Y = 8f;

				// slope / step handling (MANDATORY)
				Collision.StepUp(
					ref Projectile.position,
					ref Projectile.velocity,
					Projectile.width,
					Projectile.height,
					ref Projectile.stepSpeed,
					ref Projectile.gfxOffY
				);

				bool onGround = false;

				int tileXLeft = (int)(Projectile.position.X / 16f);
				int tileXRight = (int)((Projectile.position.X + Projectile.width) / 16f);
				int tileY = (int)((Projectile.position.Y + Projectile.height + 1) / 16f);

				for (int x = tileXLeft; x <= tileXRight; x++)
				{
					Tile tile = Main.tile[x, tileY];
					if (tile != null && tile.HasTile)
					{
						bool solidBlock = Main.tileSolid[tile.TileType];
						bool platform = Main.tileSolidTop[tile.TileType] && tile.TileFrameY == 0;

						if (solidBlock || platform)
						{
							onGround = true;
							break;
						}
					}
				}

				// === ATTACK ===
				if (foundTarget && distanceFromTarget < 120f) {
					float dirX = Math.Sign(targetCenter.X - Projectile.Center.X);

					Projectile.velocity.X = MathHelper.Lerp(
						Projectile.velocity.X,
						dirX * walkSpeed,
						acceleration
					);

					if (onGround && targetCenter.Y < Projectile.Center.Y - 16f) {
						Projectile.velocity.Y = normalJump;
					}
				}
				// === FOLLOW PLAYER ===
				else {
					// horizontal follow
					if (distanceToIdlePosition > 50f) {
						float dirX = Math.Sign(vectorToIdlePosition.X);

						Projectile.velocity.X = MathHelper.Lerp(
							Projectile.velocity.X,
							dirX * (walkSpeed * 0.8f),
							acceleration
						);
					}
					else {
						Projectile.velocity.X *= 0.8f;
						if (Math.Abs(Projectile.velocity.X) < 0.05f)
							Projectile.velocity.X = 0f;
					}

					// jump higher if player is above
					Player owner = Main.player[Projectile.owner];

					// jump if player is above AND either:
					// - player is jumping/falling
					// - OR we are blocked by terrain
					if (
						onGround &&
						vectorToIdlePosition.Y < -32f && // only 2 tiles now
						(
							owner.velocity.Y != 0f || // player jumped
							Math.Abs(vectorToIdlePosition.X) < 40f // stuck under player
						)
					) {
						Projectile.velocity.Y = highJump;
					}

					// emergency teleport if completely failed to follow
					if (distanceToIdlePosition > 600f) {
						Projectile.Center = Main.player[Projectile.owner].Center;
						Projectile.velocity = Vector2.Zero;
						Projectile.netUpdate = true;
					}
				}
			}

		private void Visuals() {
	// Face direction
	if (Projectile.velocity.X != 0f)
		Projectile.spriteDirection = Projectile.velocity.X > 0 ? 1 : -1;

	Projectile.rotation = 0f;

	int frameSpeed;
	int frameStart;
	int frameCount;

	switch (animState) {
		case AnimState.Idle:
			frameStart = 8;
			frameCount = 5;
			frameSpeed = 10;
			break;

		case AnimState.Walk:
			frameStart = 8;
			frameCount = 5;
			frameSpeed = 20;
			break;

		case AnimState.Jump:
			frameStart = 8;
			frameCount = 5;
			frameSpeed = 10;
			break;        

		default:
			return;
	}

	// Reset frame if animation state changed
	if (animState != previousAnimState) {
		Projectile.frame = frameStart;
		Projectile.frameCounter = 0;
	}

	Projectile.frameCounter++;

	if (Projectile.frameCounter >= frameSpeed) {
		Projectile.frameCounter = 0;
		Projectile.frame++;

		if (Projectile.frame < frameStart ||
			Projectile.frame >= frameStart + frameCount) {
			Projectile.frame = frameStart;
		}
	}
}

	}
}