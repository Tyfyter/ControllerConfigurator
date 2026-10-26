using PegasusLib;
using System;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace ControllerConfigurator; 
public class GetGotoKeybindKeybind : AutoModCall {
	public static ModKeybind Call() => ControllerConfigurator.goToKeybindKeybind;
	public static string Call(HelpEnum _) =>
		"Returns the \"Go To Keybind\" keybind";
}
public class OpenKeybindsToSearch : AutoModCall {
	public static void Call(ModKeybind keybind) => Call(keybind.DisplayName?.Value);
	public static void Call(string keybind) {
		SoundEngine.PlaySound(SoundID.MenuOpen);
		ControlsMenuSearch.nextSearch = keybind;
		IngameFancyUI.OpenKeybinds();
	}
	public static string Call(HelpEnum _) =>
		"Opens the controls menu and searches for the specified keybind";
}
public class RegisterKeybindCondition : AutoModCall {
	public static void Call(ModKeybind keybind, Func<bool> condition) {
		string name = keybind.FullName();
		ChordBindings.canBlock.TryGetValue(name, out Func<bool> existing);
		ChordBindings.canBlock[name] = existing + condition;
	}
	public static string Call(HelpEnum _) =>
		"Registers a condition which must be fulfilled for the provided keybind to block normal keybinds from being triggered by the key, if it is bound to a chord";
}
public enum HelpEnum {
	Help
}