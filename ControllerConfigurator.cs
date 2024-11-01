using Mono.Cecil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace ControllerConfigurator {
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ControllerConfigurator : Mod {
		public override void Load() {
			IL_Player.SelectionRadial.Update += SelectionRadial_Update;
			IL_TriggersSet.CopyInto += IL_TriggersSet_CopyInto;
		}
		private static void IL_TriggersSet_CopyInto(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.After,
				i => i.MatchCall<PlayerInput>("get_" + nameof(PlayerInput.CursorIsBusy)),
				i => i.MatchBrtrue(out _)
			);
			c.Index--;
			c.EmitDelegate((bool val) => val && !ControllerConfiguratorConfig.Instance.DisableLeftStickInRadial);
		}
		private static void SelectionRadial_Update(ILContext il) {
			ILCursor c = new(il);
			ILLabel label = default;
			c.GotoNext(MoveType.After,
				i => i.MatchBneUn(out label),
				i => i.MatchLdsfld<PlayerInput>(nameof(PlayerInput.GamepadThumbstickLeft))
			);
			c.Index--;
			c.EmitLdsfld(typeof(ControllerConfiguratorConfig).GetField(nameof(ControllerConfiguratorConfig.Instance)));
			c.EmitCall(typeof(ControllerConfiguratorConfig).GetProperty(nameof(ControllerConfiguratorConfig.DisableLeftStickInRadial)).GetGetMethod());
			c.EmitBrtrue(label);
		}
	}
	public class ControllerConfiguratorConfig : ModConfig {
		public override ConfigScope Mode => ConfigScope.ClientSide;
		public static ControllerConfiguratorConfig Instance;
		[DefaultValue(true)]
		public bool DisableLeftStickInRadial { get; set; }
	}
}
