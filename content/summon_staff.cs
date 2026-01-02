using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace shahdee_mod.content
{
	public class summon_staff : ModItem
	{

		public override string Texture =>
            "shahdee_mod/assets/summon_staff";

			
		public override void SetStaticDefaults() {
			ItemID.Sets.GamepadWholeScreenUseRange[Type] = true; // This lets the player target anywhere on the whole screen while using a controller
			ItemID.Sets.LockOnIgnoresCollision[Type] = true;

			ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f; // The default value is 1, but other values are supported. See the docs for more guidance. 
		}

		public override void SetDefaults() {
			Item.damage = 6;
			Item.knockBack = 1f;
			Item.mana = 10; // mana cost
			Item.width = 32;
			Item.height = 32;
			Item.useTime = 36;
			Item.useAnimation = 36;
			Item.useStyle = ItemUseStyleID.Swing; // how the player's arm moves when using the item
			Item.value = Item.sellPrice(gold: 30);
			Item.rare = ItemRarityID.Cyan;
			Item.UseSound = SoundID.Item44; // What sound should play when using the item

			// These below are needed for a minion weapon
			Item.noMelee = true; // this item doesn't do any melee damage
			Item.DamageType = DamageClass.Melee; // Makes the damage register as summon. If your item does not have any damage type, it becomes true damage (which means that damage scalars will not affect it). Be sure to have a damage type
			Item.buffType = ModContent.BuffType<buff>();
			// No buffTime because otherwise the item tooltip would say something like "1 minute duration"
			Item.shoot = ModContent.ProjectileType<minion>(); // This item creates the minion projectile
		}

		public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
			// Here you can change where the minion is spawned. Most vanilla minions spawn at the cursor position, limited by the gameplay range
			position = Main.MouseWorld;
			player.LimitPointToPlayerReachableArea(ref position);
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
			// This is needed so the buff that keeps your minion alive and allows you to despawn it properly applies
			player.AddBuff(Item.buffType, 2);

			return true; // The minion projectile will be spawned by the game since we return true.
		}
	}
}