using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PegasusLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Gamepad;

namespace ControllerConfigurator {
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class ControllerConfigurator : Mod {
		public static ModKeybind forceMouseKeybind;
		public static ModKeybind goToKeybindKeybind;
		public static bool forceMouse;
		public override void Load() {
			forceMouseKeybind = KeybindLoader.RegisterKeybind(this, "UseMouseWithControler", Keys.None);
			goToKeybindKeybind = KeybindLoader.RegisterKeybind(this, "GoToKeybind", Keys.OemQuestion);
			IL_Player.SelectionRadial.Update += SelectionRadial_Update;
			IL_TriggersSet.CopyInto += IL_TriggersSet_CopyInto;
			IL_Main.DrawCursor += IL_Main_DrawCursor;
			IL_Main.DrawThickCursor += IL_Main_DrawCursor;
			On_PlayerInput.GamePadInput += On_PlayerInput_GamePadInput;
			IL_PlayerInput.GamePadInput += IL_PlayerInput_GamePadInput;
			IL_UILinkPointNavigator.Update += IL_UILinkPointNavigator_Update;
			IL_ItemSlot.OverrideHover_ItemArray_int_int += CreateForceMouseHook(
				(i => i.MatchCall(typeof(ItemSlot), "get_" + nameof(ItemSlot.NotUsingGamepad)), (bool val) => val || forceMouse)
			);
			IL_ItemSlot.MouseHover_ItemArray_int_int += IL_ItemSlot_Handle_ItemArray_int_int;
		}
		public override object Call(params object[] args) {
			switch (((string)args[0]).ToUpperInvariant()) {
				case "GETGOTOKEYBINDKEYBIND":
				return goToKeybindKeybind;

				case "OPENKEYBINDSTOSEARCH":
				SoundEngine.PlaySound(SoundID.MenuOpen);
				ControlsMenuSearch.nextSearch = ((args[1] as ModKeybind)?.DisplayName?.Value) ?? args[1].ToString();
				IngameFancyUI.OpenKeybinds();
				return null;
			}
			return null;
		}
		private static void IL_ItemSlot_Handle_ItemArray_int_int(ILContext il) {
			ILCursor c = new(il);
			c.EmitLdarg0();
			c.EmitLdarg1();
			c.EmitLdarg2();
			c.EmitCall(typeof(ItemSlot).GetMethod("GetGamepadPointForSlot", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
			c.EmitDelegate((int value) => {
				gamepadHoveredSlot = value;
			});
		}

		public static ILContext.Manipulator CreateForceMouseHook(params (Func<Instruction, bool> predicate, Func<bool, bool> func)[] values) {
			return (il) => {
				ILCursor c = new(il);
				for (int i = 0; i < values.Length; i++) {
					c.Index = 0;
					(Func<Instruction, bool> predicate, Func<bool, bool> func) = values[i];
					c.GotoNext(MoveType.After, predicate);
					c.EmitDelegate(func);
				}
			};
		}
		private static void IL_UILinkPointNavigator_Update(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.After,
				i => i.MatchCall(typeof(UILinkPointNavigator), "get_" + nameof(UILinkPointNavigator.Available))
			);
			c.EmitDelegate((bool val) => val && !forceMouse);
		}

		private static void IL_PlayerInput_GamePadInput(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.After,
				i => i.MatchCall(typeof(UILinkPointNavigator), "get_" + nameof(UILinkPointNavigator.Available))
			);
			c.EmitDelegate((bool val) => val && !forceMouse);
			c.GotoNext(MoveType.After,
				i => i.MatchCall<IngameFancyUI>(nameof(IngameFancyUI.CanCover))
			);
			c.EmitDelegate((bool val) => val || forceMouse);
			c.GotoNext(MoveType.After,
				i => i.MatchCall(typeof(PlayerInput), "get_" + nameof(PlayerInput.CursorIsBusy))
			);
			c.EmitDelegate((bool val) => val || forceMouse);
		}

		static int gamepadHoveredSlot = -1;
		private static bool On_PlayerInput_GamePadInput(On_PlayerInput.orig_GamePadInput orig) {
			bool value = orig();
			if (PlayerInput.Triggers.Old.KeyStatus.ContainsKey($"{nameof(ControllerConfigurator)}/UseMouseWithControler") && !forceMouseKeybind.Old && forceMouseKeybind.Current) {
				forceMouse ^= true;
				//PlayerInput.UseSteamDeckIfPossible = forceMouse || SteamUtils.IsSteamRunningOnSteamDeck();
				PlayerInput.PreventCursorModeSwappingToGamepad = forceMouse;
				if (forceMouse) {
					PlayerInput.SettingsForUI.SetCursorMode(CursorMode.Mouse);
					if (UILinkPointNavigator.Pages.TryGetValue(UILinkPointNavigator.CurrentPage, out UILinkPage page) && UILinkPointNavigator.Points.TryGetValue(page.CurrentPoint, out UILinkPoint point)) {
						PlayerInput.MouseX = (int)point.Position.X;
						PlayerInput.MouseY = (int)point.Position.Y;
						PlayerInput.PreUIX = (int)point.Position.X;
						PlayerInput.PreUIY = (int)point.Position.Y;
					}
				} else {
					UILinkPointNavigator.InUse = true;
					UILinkPointNavigator.ChangePoint(gamepadHoveredSlot);
				}
			}
			if (forceMouse && !Main.playerInventory && !Main.ingameOptionsWindow && !(Main.InGameUI.IsVisible && (Main.InGameUI.CurrentState == Main.ManageControlsMenu || Main.InGameUI.CurrentState == Main.AchievementsMenu))) {
				forceMouse = false;
			} else if (!forceMouse && ChordBindings.HasBindingWindowOpen) {
				forceMouse = true;
			}
			if (forceMouse) {
				Vector2 direction = PlayerInput.GamepadThumbstickRight;
				direction.X = SubtractBlockChangeSign(direction.X, PlayerInput.CurrentProfile.LeftThumbstickDeadzoneX * Math.Sign(direction.X));
				direction.Y = SubtractBlockChangeSign(direction.Y, PlayerInput.CurrentProfile.LeftThumbstickDeadzoneY * Math.Sign(direction.Y));
				//direction.X = ControllerConfiguratorConfig.Instance.ControlerMouseSensitivityCurve[direction.X] * Math.Sign(direction.X);
				//direction.Y = ControllerConfiguratorConfig.Instance.ControlerMouseSensitivityCurve[direction.Y] * Math.Sign(direction.Y);
				direction *= ControllerConfiguratorConfig.Instance.ControlerMouseSensitivity;
				PlayerInput.MouseX += (int)direction.X;
				PlayerInput.MouseY += (int)direction.Y;
				PlayerInput.MouseX = Math.Clamp(PlayerInput.MouseX, 0, Main.screenWidth);
				PlayerInput.MouseY = Math.Clamp(PlayerInput.MouseY, 0, Main.screenHeight);
			}
			return value;
		}
		static float SubtractBlockChangeSign(float a, float b) {
			float v = a - b;
			if (Math.Sign(v) != Math.Sign(a)) return 0;
			return v;
		}
		private static void IL_Main_DrawCursor(ILContext il) {
			ILCursor c = new(il);
			while (c.TryGotoNext(MoveType.After,
				i => i.MatchCall(typeof(PlayerInput.SettingsForUI), "get_" + nameof(PlayerInput.SettingsForUI.ShowGamepadCursor))
			)) {
				c.EmitDelegate((bool val) => val && !forceMouse);
			}
		}

		private static void IL_TriggersSet_CopyInto(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.After,
				i => i.MatchLdsfld<PlayerInput>(nameof(PlayerInput.CurrentInputMode))
			);
			c.EmitDelegate((InputMode val) => {
				if (forceMouse && val == InputMode.XBoxGamepadUI) val = InputMode.XBoxGamepad;
				return val;
			});

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
		[DefaultValue(8f), Range(0f, 128f)]
		public float ControlerMouseSensitivity { get; set; }
		/*[CustomModConfigItem(typeof(JoystickSensitivityCurveElement))]
		[JsonDefaultValue("[]")]
		public FloatCurve ControlerMouseSensitivityCurve { get; set; } = [
			new FloatCurve.LinearNode(),
			new FloatCurve.LinearNode() {
				x = 1,
				y = 1
			}
		];*/
	}

	public class JoystickSensitivityCurveConverter : JsonConverter {
		public override bool CanConvert(Type objectType) {
			throw new NotImplementedException();
		}

		public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
			throw new NotImplementedException();
		}

		public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
			throw new NotImplementedException();
		}
	}
	public class JoystickSensitivityCurveElement : ConfigElement<FloatCurve> {
		public FloatCurve NewValue {
			get {
				if (!ValueChanged) Value = (FloatCurve)Value.Clone();
				return Value;
			}
		}
		Curve<float>.CurveNode dragNode = null;
		public override void OnBind() {
			base.OnBind();
			Height.Pixels = 64 + 8;
		}
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			base.DrawSelf(spriteBatch);
			Rectangle bounds = GetInnerDimensions().ToRectangle();
			Rectangle area = new(bounds.Right - (64 + 4), bounds.Y + 4, 64, 64);
			Rectangle frame = new(0, 0, 1, 1);
			bool clicked = false;
			if (Main.mouseLeft && Main.mouseLeftRelease && area.Contains(Main.MouseScreen.ToPoint())) {
				clicked = true;
				_ = NewValue;
			} else if (dragNode is not null) {
				if (Main.mouseLeftRelease) {
					dragNode = null;
				} else {
					dragNode.x = Math.Clamp((Main.mouseX - area.X) / (float)area.Width, 0, 1);
					dragNode.y = Math.Clamp(1 - (Main.mouseY - area.Y) / (float)area.Height, 0, 1);
					Value.Reorder(dragNode);
				}
			}
			spriteBatch.Draw(
				TextureAssets.MagicPixel.Value,
				area,
				Color.Black
			);
			for (int i = 0; i < area.Width; i++) {
				int amount = (int)(Value[i / (float)area.Width] * area.Height);
				frame.X = area.X + i;
				frame.Y = area.Y + area.Height - amount;
				frame.Height = amount;
				spriteBatch.Draw(
					TextureAssets.MagicPixel.Value,
					frame,
					new Color(i / (float)area.Width, 0, amount / (float)area.Height)
				);
			}
			Rectangle dot = new(0, 0, 2, 2);
			Curve<float>.CurveNode clickedNode = null;
			float dist = 8;
			foreach (Curve<float>.CurveNode node in Value) {
				Vector2 pos = area.TopLeft() + area.Size() * new Vector2(node.x, 1 - node.y);
				spriteBatch.Draw(
					TextureAssets.MagicPixel.Value,
					pos,
					dot,
					Color.Black,
					MathHelper.PiOver4,
					Vector2.One,
					3f,
					SpriteEffects.None,
				0);
				spriteBatch.Draw(
					TextureAssets.MagicPixel.Value,
					area.TopLeft() + area.Size() * new Vector2(node.x, 1 - node.y),
					dot,
					Color.Goldenrod,
					MathHelper.PiOver4,
					Vector2.One,
					2f,
					SpriteEffects.None,
				0);
				if (clicked && pos.IsWithin(Main.MouseScreen, dist)) {
					clickedNode = node;
				}
			}
			if (clicked && clickedNode is null) {
				Value.Add(new FloatCurve.LinearNode() {
					x = (Main.mouseX - area.X) / (float)area.Width,
					y = 1 - (Main.mouseY - area.Y) / (float)area.Height
				});
			}
			if (clickedNode is not null) {
				dragNode = clickedNode;
			}
		}
	}
	public class FloatCurve : Curve<float> {
		public override float Interpolate(float a, float b, float progress) => Utils.Remap(progress, 0, 1, a, b);
	}
	public abstract class Curve<T> : IEnumerable<Curve<T>.CurveNode> {
		List<CurveNode> nodes = [];
		public Curve<T> Clone() {
			Curve<T> clone = (Curve<T>)MemberwiseClone();
			clone.nodes = [..nodes];
			return clone;
		}
		public T this[float position] {
			get {
				CurveNode prevNode = default;
				CurveNode currentNode = default;
				for (int i = 0; i < nodes.Count; i++) {
					if (nodes[i].x == position) return nodes[i].y;
					prevNode = currentNode;
					currentNode = nodes[i];
					if (currentNode.x > position) break;
				}
				if (currentNode == null) return default;
				if (position > currentNode.x || prevNode is null) return currentNode.y;
				return Interpolate(prevNode.y, currentNode.y, currentNode.GetProgress(position, prevNode.x, currentNode.x));
			}
		} 
		public void Add(CurveNode node) {
			nodes.InsertOrdered(node);
		}
		public void Reorder(CurveNode node) {
			nodes.Remove(node);
			nodes.InsertOrdered(node);
		}
		public abstract T Interpolate(T a, T b, float progress);
		public class LinearNode : CurveNode {
			public override float GetProgress(float x, float aX, float bX) => Utils.Remap(x, aX, bX, 0, 1);
		}
		public abstract class CurveNode : IComparable<CurveNode> {
			public bool Locked { get; init; }  = false;
			public float x;
			public T y;
			public abstract float GetProgress(float aX, float bX, float x);
			public int CompareTo(CurveNode other) => x.CompareTo(other.x);
		}

		public IEnumerator<CurveNode> GetEnumerator() {
			return ((IEnumerable<CurveNode>)nodes).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return ((IEnumerable)nodes).GetEnumerator();
		}
	}
}
