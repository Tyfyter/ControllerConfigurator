using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.GameContent.UI.Elements;
using Terraria;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.UI;
using PegasusLib;
using System.Reflection;
using Terraria.GameContent.UI.Chat;
using Terraria.GameContent.UI.States;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader.UI;
using Terraria.Localization;
using System.ComponentModel;
using Terraria.GameContent;
using Terraria.UI.Chat;
using Terraria.Audio;
using Terraria.ID;
using Microsoft.Build.Tasks;

namespace ControllerConfigurator {
	public class ChordBindings : ILoadable {
		public FastFieldInfo<UIKeybindingListItem, string> _keybind = new("_keybind", BindingFlags.Public | BindingFlags.NonPublic);
		public void Load(Mod mod) {
			On_KeyConfiguration.Processkey += On_KeyConfiguration_Processkey;
			On_UIKeybindingListItem.ctor += On_UIKeybindingListItem_ctor;
			On_GlyphTagHandler.GenerateTag_string += On_GlyphTagHandler_GenerateTag_string;
			On_UIManageControls.AssembleBindPanels += On_UIManageControls_AssembleBindPanels;
			On_PlayerInput.CheckRebindingProcessGamepad += On_PlayerInput_CheckRebindingProcessGamepad;
		}
		private bool On_PlayerInput_CheckRebindingProcessGamepad(On_PlayerInput.orig_CheckRebindingProcessGamepad orig, string newKey) {
			if (chordBindingElement?.listeningChordPart is not null) {
				chordBindingElement.listeningChordPart.TryBind(newKey);
				return true;
			}
			return orig(newKey);
		}
		public static bool HasBindingWindowOpen => ModContent.GetInstance<ChordBindings>().chordBindingElement is ChordBindingElement chordBindingElement && chordBindingElement.Visible;
		ChordBindingElement chordBindingElement;
		private void On_UIManageControls_AssembleBindPanels(On_UIManageControls.orig_AssembleBindPanels orig, UIManageControls self) {
			orig(self);
			self.Append(chordBindingElement = new());
		}

		private string On_GlyphTagHandler_GenerateTag_string(On_GlyphTagHandler.orig_GenerateTag_string orig, string keyname) {
			string[] parts = keyname.Split('+');
			if (parts.Length > 1) {
				return string.Join("+", parts.Select(s => {
					string prefix = "";
					if (s.StartsWith('-')) {
						s = s[1..];
						prefix = "-";
					}
					return prefix + orig(s);
				}));
			}
			return orig(keyname);
		}

		private void On_UIKeybindingListItem_ctor(On_UIKeybindingListItem.orig_ctor orig, UIKeybindingListItem self, string bind, InputMode mode, Color color) {
			orig(self, bind, mode, color);
			self.OnRightClick += (_, _) => {
				chordBindingElement.SetBinding(bind, mode);
			};
		}
		private void On_KeyConfiguration_Processkey(On_KeyConfiguration.orig_Processkey orig, KeyConfiguration self, TriggersSet set, string newKey, InputMode mode) {
			GamePadState gamePadState = default(GamePadState);
			for (int i = 0; i < 4; i++) {
				GamePadState state = GamePad.GetState((PlayerIndex)i);
				if (state.IsConnected) {
					gamePadState = state;
					break;
				}
			}
			bool didAnyCombos = false;
			foreach (KeyValuePair<string, List<string>> item in self.KeyStatus) {
				foreach (string binding in item.Value) {
					string[] parts = binding.Split('+');
					if (parts.Length > 1 && parts[^1] == newKey) {
						if (PlayerInput.CurrentInputMode is InputMode.XBoxGamepad or InputMode.XBoxGamepadUI) {
							for (int i = 0; i < parts.Length - 1; i++) {
								string part = parts[i];
								bool inverted = false;
								if (part.StartsWith('-')) {
									part = part[1..];
									inverted = true;
								}
								if (!Enum.TryParse(part, true, out Buttons buttons) || (gamePadState.IsButtonUp(buttons) ^ inverted)) {
									goto broken;
								}
							}
						} else {
							for (int i = 0; i < parts.Length - 1; i++) {
								string part = parts[i];
								bool inverted = false;
								if (part.StartsWith('-')) {
									part = part[1..];
									inverted = true;
								}
								if (!Enum.TryParse(part, true, out Keys keys) || (Main.keyState.IsKeyUp(keys) ^ inverted)) {
									goto broken;
								}
							}
						}
						set.KeyStatus[item.Key] = true;
						set.LatestInputMode[item.Key] = mode;
						didAnyCombos = true;
					}
					broken:;
				}
			}
			if (!didAnyCombos) {
				orig(self, set, newKey, mode);
			} else if (!set.UsedMovementKey) {
				if (set.Up || set.Down || set.Left || set.Right || set.HotbarPlus || set.HotbarMinus || ((Main.gameMenu || Main.ingameOptionsWindow) && (set.MenuUp || set.MenuDown || set.MenuLeft || set.MenuRight))) {
					set.UsedMovementKey = true;
				}
			}
		}
		public void Unload() {}
	}
	public class ChordBindingElement : UIPanel {
		InputMode mode;
		List<string> realBindings;
		List<List<string>> bindingLists;
		bool visible = false;
		public bool Visible => visible;
		ManualList bindingList;
		internal UIChordbindingListItem listeningChordPart;
		public bool IsForGamepad => mode is InputMode.XBoxGamepad or InputMode.XBoxGamepadUI;
		public void SetBinding(string bind, InputMode mode) {
			this.mode = mode;
			visible = true;
			if (PlayerInput.CurrentProfile.InputModes.TryGetValue(mode, out KeyConfiguration config) && config.KeyStatus.TryGetValue(bind, out List<string> bindings)) {
				this.realBindings = bindings;
				ImportBindingLists();
			} else {
				this.realBindings = null;
				visible = false;
			}
			SetupList();
			this.Recalculate();
		}
		public override void OnInitialize() {
			Width.Set(0, 0.35f);
			Height.Set(0, 0.5f);
			VAlign = 0.65f;
			HAlign = 0.5f;

			bindingList = new();
			//bindingList.Top.Set(0, 0.1f);
			bindingList.Width.Set(0, 1);
			//bindingList.MaxHeight.Set(-32, 1);
			//bindingList.MinHeight.Set(32, 0);
			bindingList.OverflowHidden = false;
			Append(bindingList);

			UIButton<LocalizedText> saveButton = new(Language.GetOrRegister("LegacyInterface.47")) {
				VAlign = 1
			};
			saveButton.Width.Set(0, 0.5f);
			saveButton.Height.Set(32, 0);
			saveButton.OnLeftClick += (_, _) => {
				this.visible = false;
				ExportBindingLists();
			};
			Append(saveButton);

			UIButton<LocalizedText> closeButton = new(Language.GetOrRegister("LegacyInterface.63")) {
				VAlign = 1,
				HAlign = 1
			};
			closeButton.Width.Set(0, 0.5f);
			closeButton.Height.Set(32, 0);
			closeButton.OnLeftClick += (_, _) => {
				this.visible = false;
			};
			Append(closeButton);
		}
		public override void Update(GameTime gameTime) {
			base.Update(gameTime);
		}
		public void SetupList() {
			bindingList.Clear();
			if (bindingLists.Count == 0) bindingLists.Add([]);
			for (int i = 0; i < bindingLists.Count; i++) {
				if (i != 0) {
					bindingList.Add(new UIHorizontalSeparator());
				}
				ManualList list = new();
				list.Width.Set(0, 1);
				//list.Top.Set(0, 0.1f);
				//list.MinHeight.Set(32, 0);
				list.OverflowHidden	= false;
				FillBindingElement(list, i);
				bindingList.Add(list);
			}
		}
		void FillBindingElement(ManualList list, int bindingIndex) {
			list.Clear();
			List<string> parts = bindingLists[bindingIndex];
			if (parts.Count > 0) {
				UIButton<string> insertButton = new("+");
				insertButton.Width.Set(0, 1f);
				insertButton.Height.Set(32, 0);
				insertButton.OnLeftClick += (_, _) => {
					parts.Insert(0, "");
					FillBindingElement(list, bindingIndex);
				};
				list.Add(insertButton);
			}
			for (int i = 0; i < parts.Count; i++) {
				list.Add(new UIChordbindingListItem(this, list, bindingIndex, i) {
					Height = new(32, 0),
					VAlign = 0
				});
			}
			UIButton<string> addButton = new("+");
			addButton.Width.Set(0, 1f);
			addButton.Height.Set(32, 0);
			addButton.OnLeftClick += (_, _) => {
				parts.Add("");
				FillBindingElement(list, bindingIndex);
			};
			list.Add(addButton);
		}
		void ImportBindingLists() {
			bindingLists = realBindings.Select(s => s.Split('+').ToList()).ToList();
		}
		void ExportBindingLists() {
			realBindings.Clear();
			realBindings.AddRange(bindingLists.Select(s => string.Join("+", s.Where(s => !string.IsNullOrEmpty(s)))));
		}
		public override bool ContainsPoint(Vector2 point) {
			if (!visible) return false;
			return base.ContainsPoint(point);
		}
		public override void Draw(SpriteBatch spriteBatch) {
			if (!visible) return;
			base.Draw(spriteBatch);
		}
		public class ManualList : UIElement {
			public ManualList() {
				PaddingRight = 0;
				PaddingLeft = 0;
				PaddingBottom = 0;
				PaddingTop = 0;
			}
			public void Clear() {
				RemoveAllChildren();
				this.Height.Pixels = 0;
				Recalculate();
			}
			public void Add(UIElement element) {
				element.Top.Set(this.GetInnerDimensions().Height + 4, 0);
				element.Height.Set(element.Height.Pixels, 0);
				Append(element);
				this.Height.Pixels += element.Height.Pixels + 4;
				Recalculate();
			}
		}
		public class UIChordbindingListItem : UIElement {
			ChordBindingElement parent;
			ManualList elementContainer;
			int bindingIndex;
			int partIndex;
			public UIChordbindingListItem(ChordBindingElement parent, ManualList elementContainer, int bindingIndex, int partIndex) {
				this.Width.Set(0, 1);
				this.Height.Set(32, 0);
				PaddingRight = 0;
				PaddingLeft = 0;
				PaddingBottom = 0;
				PaddingTop = 0;
				this.parent = parent;
				this.elementContainer = elementContainer;
				this.bindingIndex = bindingIndex;
				this.partIndex = partIndex;
				string binding = Binding[partIndex];
				if (binding.StartsWith('-')) {
					binding = binding[1..];
				}
				UIButton<string> button = new(parent.IsForGamepad && binding.Length > 0 ? $"[g:{binding}]" : binding);
				button.Left.Set(0, 0.10f);
				button.Width.Set(0, 0.80f);
				button.Height.Set(0, 1);
				button.OnLeftClick += (_, _) => {
					if (parent.listeningChordPart != this) {
						parent.listeningChordPart = this;
					} else {
						parent.listeningChordPart = null;
					}
				};
				Append(button);
				UIButton<string> negateButton = new("-");
				negateButton.Width.Set(0, 0.10f);
				negateButton.Height.Set(0, 1);
				negateButton.AltPanelColor = Color.Gold;
				negateButton.AltHoverPanelColor = Color.Goldenrod;
				negateButton.UseAltColors = () => Binding[partIndex].StartsWith('-');
				negateButton.OnLeftClick += (_, _) => {
					if (Binding[partIndex].StartsWith('-')) {
						Binding[partIndex] = Binding[partIndex][1..];
					} else {
						Binding[partIndex] = "-" + Binding[partIndex];
					}
					parent.FillBindingElement(elementContainer, bindingIndex);
				};
				Append(negateButton);
				UIButton<string> removeButton = new("X") {
					HAlign = 1
				};
				removeButton.Width.Set(0, 0.10f);
				removeButton.Height.Set(0, 1);
				removeButton.OnLeftClick += (_, _) => {
					Binding.RemoveAt(partIndex);
					parent.FillBindingElement(elementContainer, bindingIndex);
				};
				Append(removeButton);
				this.Initialize();
			}
			public List<string> Binding => parent.bindingLists[bindingIndex];
			public void TryBind(string newKey) {
				SoundEngine.PlaySound(SoundID.MenuTick);
				if (Binding[partIndex] != newKey) {
					Binding[partIndex] = newKey;
					parent.FillBindingElement(elementContainer, bindingIndex);
				}
				parent.listeningChordPart = null;
			}
		}
	}
}
