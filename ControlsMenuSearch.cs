using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoMod.Cil;
using PegasusLib;
using PegasusLib.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.UI;
using Terraria.UI;

namespace ControllerConfigurator {
	public class ControlsMenuSearch : ILoadable {
		public static string nextSearch = null;
		public static string search = null;
		FastFieldInfo<UIManageControls, UIKeybindingSimpleListItem> _buttonProfile = new("_buttonProfile", BindingFlags.NonPublic);
		FastStaticFieldInfo<KeybindLoader, IDictionary<string, ModKeybind>> modKeybinds = new ("modKeybinds", BindingFlags.NonPublic);
		FastFieldInfo<UIManageControls, bool> OnKeyboard = new("OnKeyboard", BindingFlags.NonPublic);
		FastFieldInfo<UIManageControls, bool> OnGameplay = new("OnGameplay", BindingFlags.NonPublic);
		static Action UIManageControls_FillList;
		static Func<Mod> ModKeybind_GetMod;
		public void Load(Mod mod) {
			On_UIManageControls.OnInitialize += On_UIManageControls_OnInitialize;
			On_UIManageControls.OnActivate += On_UIManageControls_OnActivate;
			IL_UIManageControls.FillList += IL_UIManageControls_FillList;
			UIManageControls_FillList = typeof(UIManageControls).GetMethod("FillList", BindingFlags.NonPublic | BindingFlags.Instance).CreateDelegate<Action>(new UIManageControls());
			PropertyInfo prop = typeof(ModKeybind).GetProperty("Mod", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			ConstructorInfo ctor = typeof(ModKeybind).GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, [typeof(Mod), typeof(string), typeof(string)]);
			ModKeybind_GetMod = prop.GetMethod.CreateDelegate<Func<Mod>>(
				ctor.Invoke([ModContent.GetInstance<ControllerConfigurator>(), "FakeKeybind", ""])
			);
		}
		public static void FillList(UIManageControls instance) {
			PegasusLib.Reflection.DelegateMethods._target.SetValue(UIManageControls_FillList, instance);
			UIManageControls_FillList();
		}
		public static Mod GetMod(ModKeybind instance) {
			PegasusLib.Reflection.DelegateMethods._target.SetValue(ModKeybind_GetMod, instance);
			return ModKeybind_GetMod();
		}
		public void Unload() {
			UIManageControls_FillList = null;
			ModKeybind_GetMod = null;
		}
		private void IL_UIManageControls_FillList(ILContext il) {
			ILCursor c = new(il);
			c.GotoNext(MoveType.Before, i => i.MatchCallvirt<List<UIElement>>(nameof(List<UIElement>.GetEnumerator)));
			c.EmitLdarg0();
			c.EmitDelegate<Func<List<UIElement>, UIManageControls, List<UIElement>>>((list, self) => {
				if (search is not null) {
					int snapPointIndex = 0;
					List<UIElement> newList = [];
					foreach (KeyValuePair<string, List<string>> item in PlayerInput.CurrentProfile.InputModes[GetInputMode(self)].KeyStatus) {
						if (!MatchesSearch(item.Key)) continue;
						UIElement uIElement2 = self.CreatePanel(item.Key, InputMode.Keyboard, Color.Firebrick);
						uIElement2.Width.Set(0f, 1f);
						uIElement2.Height.Set(0f, 1f);
						uIElement2.SetSnapPoint("Wide", snapPointIndex++);
						UISortableElement sortableElement = new(snapPointIndex - 1);
						sortableElement.Width.Set(0f, 1f);
						sortableElement.Height.Set(30f, 0f);
						sortableElement.MarginBottom = -16;
						sortableElement.Append(uIElement2);
						newList.Add(sortableElement);
					}
					return newList;
				}
				return list;
			});
		}
		InputMode GetInputMode(UIManageControls uiManageControls) {
			switch ((OnKeyboard.GetValue(uiManageControls), OnGameplay.GetValue(uiManageControls))) {
				case (true, true):
				return InputMode.Keyboard;

				case (true, false):
				return InputMode.KeyboardUI;

				case (false, true):
				return InputMode.XBoxGamepad;

				case (false, false):
				return InputMode.XBoxGamepadUI;
			}
		}
		bool MatchesSearch(string keybind) {
			if (modKeybinds.Value.TryGetValue(keybind, out ModKeybind modKeybind)) {
				if (modKeybind.DisplayName.Value.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return true;
				if (GetMod(modKeybind).DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return true;
				if (keybind.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return true;
				if (GetMod(modKeybind).Name.Contains(search, StringComparison.CurrentCultureIgnoreCase)) return true;
			}
			return false;
		}
		ControlsMenuSearchElement element;
		private void On_UIManageControls_OnActivate(On_UIManageControls.orig_OnActivate orig, UIManageControls self) {
			search = nextSearch;
			nextSearch = null;
			if (element is not null) {
				if (search is null) {
					element.searchText = null;
					element.CursorIndex = 0;
					element.focused = false;
				} else {
					element.searchText = new(search);
					element.CursorIndex = search.Length;
					element.focused = false;
				}
			}
			orig(self);
		}
		private void On_UIManageControls_OnInitialize(On_UIManageControls.orig_OnInitialize orig, UIManageControls self) {
			orig(self);
			UIKeybindingSimpleListItem buttonProfile = _buttonProfile.GetValue(self);
			buttonProfile.Append(element = new ControlsMenuSearchElement() {
				Left = new(-30, 0),
				Width = new(30, 0),
				MaxWidth = new(30, 1),
				Height = new(30, 0)
			});
		}
	}
	public class ControlsMenuSearchElement : UIElement, ITextInputContainer {
		public static ref string Search => ref ControlsMenuSearch.search;
		public StringBuilder searchText = null;
		public string TextDisplay => focused ? searchText.ToString() : Search;
		public int CursorIndex { get; set; } = 0;
		public StringBuilder Text => searchText;
		public bool focused = false;
		public bool hovering = false;
		bool mouseLeftLast = false;
		bool mouseRightLast = false;
		bool mouseMiddleLast = false;
		public override void OnInitialize() {
			IgnoresMouseInteraction = false;
		}
		public override void LeftClick(UIMouseEvent evt) {
			if (TextDisplay is not null && evt.MousePosition.X - this.GetDimensions().ToRectangle().Right > -30) {
				RightClick(evt);
				return;
			}
			searchText ??= new();
			CursorIndex = searchText.Length;
			focused = true;
		}
		public override void RightClick(UIMouseEvent evt) {
			this.Clear();
			Search = null;
			focused = false;
			ControlsMenuSearch.FillList(Main.ManageControlsMenu);
		}
		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
			float targetWidth = TextDisplay is null ? 0 : 1;
			if (Width.Percent != targetWidth) {
				Width.Percent = targetWidth;
				Parent.Recalculate();
			}
			hovering = ContainsPoint(Main.MouseScreen);
			if (Main.hasFocus) {
				bool mouseButtonPressed = false;
				if (Main.mouseLeft && !mouseLeftLast) {
					mouseButtonPressed = true;
					if (hovering && !IsMouseHovering) LeftClick(new(this, Main.MouseScreen));
				} else if (Main.mouseRight && !mouseRightLast) {
					mouseButtonPressed = true;
					if (hovering && !IsMouseHovering) RightClick(new(this, Main.MouseScreen));
				} else if (Main.mouseMiddle && !mouseMiddleLast) {
					mouseButtonPressed = true;
				}
				if (mouseButtonPressed && !hovering) {
					focused = false;
				}
			}
			mouseLeftLast = Main.mouseLeft;
			mouseRightLast = Main.mouseRight;
			mouseMiddleLast = Main.mouseMiddle;
			if (focused) {
				if (searchText is null) {
					focused = false;
				} else {
					this.ProcessInput(out _);
				}
			}
		}
		AutoLoadingAsset<Texture2D> cancelbutton = "Terraria/Images/UI/SearchCancel";
		protected override void DrawSelf(SpriteBatch spriteBatch) {
			CalculatedStyle dimensions = this.GetDimensions();
			Utils.DrawSettingsPanel(spriteBatch, dimensions.Position(), dimensions.Width, hovering ? UICommon.DefaultUIBlue : UICommon.DefaultUIBlueMouseOver);
			Rectangle area = dimensions.ToRectangle();
			area.Width = area.Height;
			Rectangle clearButton = area;
			area.Inflate(-4, -4);
			spriteBatch.Draw(
				TextureAssets.Cursors[2].Value,
				area,
				Color.White * (hovering ? 1 : 0.8f)
			);
			if (TextDisplay is not null) {
				area.X += (int)dimensions.Width - 30;
				clearButton.X += (int)dimensions.Width - 30;
				spriteBatch.Draw(
					cancelbutton,
					area,
					Color.AliceBlue * (clearButton.Contains(Main.MouseScreen.ToPoint()) ? 1 : 0.8f)
				);
				Vector2 size = FontAssets.MouseText.Value.MeasureString(TextDisplay);
				float scale = Math.Min((dimensions.Width - 60) / (size.X + 1), 1.2f);
				this.DrawInputContainerText(spriteBatch,
					dimensions.Position() + new Vector2(30, dimensions.Height * 0.5f - 22 * scale * 0.5f),
					FontAssets.MouseText.Value,
					Color.White,
					focused,
					scale,
					new(8, 2)
				);
			}
		}
		void ITextInputContainer.Reset() {
			focused = false;
			searchText.Clear();
			if (Search is not null) searchText.Append(Search);
		}
		void ITextInputContainer.Submit() {
			Search = searchText.ToString();
			if (string.IsNullOrEmpty(Search)) Search = null;
			ControlsMenuSearch.FillList(Main.ManageControlsMenu);
			focused = false;
		}
	}
}
