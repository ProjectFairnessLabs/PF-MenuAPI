using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;
using static CitizenFX.Core.Native.API;
using static CitizenFX.Core.Native.Function;
using static CitizenFX.Core.Native.Hash;

namespace MenuAPI
{
    public class MenuController : BaseScript
    {
        public static List<Menu> Menus { get; protected set; } = new List<Menu>();
#if FIVEM
        public const string _texture_dict = "commonmenu";
        public const string _header_texture = "interaction_bgd";
#endif
#if REDM
        public const string _texture_dict = "menu_textures";
        public const string _header_texture = "translate_bg_1a";
#endif
        private static List<string> menuTextureAssets = new List<string>()
        {
#if FIVEM
            "commonmenu",
            "commonmenutu",
            "mpleaderboard",
            "mphud",
            "mpshopsale",
            "mpinventory",
            "mprankbadge",
            "mpcarhud",
            "mpcarhud2",
#endif
#if REDM
            "menu_textures",
            "boot_flow",
            "generic_textures",
#endif
        };

#if FIVEM
        private static float AspectRatio => GetScreenAspectRatio(false);
#endif
#if REDM
        private static float AspectRatio => 16 / 9;
#endif
        public static float ScreenWidth => 1080 * AspectRatio;
        public static float ScreenHeight => 1080;
        public static bool DisableMenuButtons { get; set; } = false;
#if FIVEM
        public static bool AreMenuButtonsEnabled => Menus.Any((m) => m.Visible) && !Game.IsPaused && CitizenFX.Core.UI.Screen.Fading.IsFadedIn && !IsPlayerSwitchInProgress() && !DisableMenuButtons && !Game.Player.IsDead;
#endif
#if REDM
        public static bool AreMenuButtonsEnabled =>
            Menus.Any((m) => m.Visible) &&
            !Call<bool>(IS_PAUSE_MENU_ACTIVE) &&
            Call<bool>(IS_SCREEN_FADED_IN) &&
            !DisableMenuButtons &&
            !Call<bool>(IS_ENTITY_DEAD, PlayerPedId());
#endif

        public static bool EnableManualGCs { get; set; } = true;
        public static bool DontOpenAnyMenu { get; set; } = false;
        public static bool PreventExitingMenu { get; set; } = false;
        public static bool DisableBackButton { get; set; } = false;
        public static Control MenuToggleKey { get; set; }
#if FIVEM
            = Control.InteractionMenu
#endif
#if REDM
            = Control.Map
#endif
            ;

        public static bool EnableMenuToggleKeyOnController { get; set; } = true;

        internal static Dictionary<MenuItem, Menu> MenuButtons { get; private set; } = new Dictionary<MenuItem, Menu>();

        public static Menu MainMenu { get; set; } = null;

#if FIVEM
        internal static int _scale = RequestScaleformMovie("INSTRUCTIONAL_BUTTONS");
#endif

        private static int ManualTimerForGC = GetGameTimer();

#if FIVEM
        private static MenuAlignmentOption _alignment = MenuAlignmentOption.Left;
        public static MenuAlignmentOption MenuAlignment
        {

            get
            {
                return _alignment;
            }
            set
            {
                if (AspectRatio < 1.888888888888889f)
                {
                    // alignment can be whatever the resource wants it to be because this aspect ratio is supported.
                    _alignment = value;
                }
                // right aligned menus are not supported for aspect ratios 17:9 or 21:9.
                else
                {
                    // no matter what the new value would've been, the aspect ratio does not support right aligned menus, 
                    // so (re)set it to be left aligned.
                    _alignment = MenuAlignmentOption.Left;

                    // In case the value was being changed to be right aligned, notify the user properly.
                    if (value == MenuAlignmentOption.Right)
                        Debug.WriteLine($"[MenuAPI ({GetCurrentResourceName()})] Warning: Right aligned menus are not supported for aspect ratios 17:9 or 21:9, left aligned will be used instead.");
                }
            }
        }

        public enum MenuAlignmentOption
        {
            Left,
            Right
        }
#endif

        /// <summary>
        /// Constructor
        /// </summary>
        public MenuController()
        {
            Tick += ProcessMenus;
#if FIVEM
            Tick += DrawInstructionalButtons;
#endif
            Tick += ProcessMainButtons;
            Tick += ProcessDirectionalButtons;
            Tick += ProcessToggleMenuButton;
            Tick += MenuButtonsDisableChecks;
        }

        /// <summary>
        /// This binds the <paramref name="childMenu"/> menu to the <paramref name="menuItem"/> and sets the menu's parent to <paramref name="parentMenu"/>.
        /// </summary>
        /// <param name="parentMenu"></param>
        /// <param name="childMenu"></param>
        /// <param name="menuItem"></param>
        public static void BindMenuItem(Menu parentMenu, Menu childMenu, MenuItem menuItem)
        {
            AddSubmenu(parentMenu, childMenu);
            if (MenuButtons.ContainsKey(menuItem))
            {
                MenuButtons[menuItem] = childMenu;
            }
            else
            {
                MenuButtons.Add(menuItem, childMenu);
            }
        }

        /// <summary>
        /// This adds the <paramref name="menu"/> <see cref="Menu"/> to the <see cref="Menus"/> list.
        /// </summary>
        /// <param name="menu"></param>
        public static void AddMenu(Menu menu)
        {
            if (!Menus.Contains(menu))
            {
                Menus.Add(menu);
                // automatically set the first menu as the main menu if none is set yet, this can be changed at any time though.
                if (MainMenu == null)
                {
                    MainMenu = menu;
                }
            }
        }

        /// <summary>
        /// Adds the <paramref name="child"/> <see cref="Menu"/> to the menus list and sets the menu's parent to <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="child"></param>
        public static void AddSubmenu(Menu parent, Menu child)
        {
            if (!Menus.Contains(child))
                AddMenu(child);
            child.ParentMenu = parent;
        }


        /// <summary>
        /// Loads the texture dict for the common menu sprites.
        /// </summary>
        /// <returns></returns>
        private static async Task LoadAssets()
        {
#if FIVEM
            menuTextureAssets.ForEach(asset =>
            {
                if (!HasStreamedTextureDictLoaded(asset))
                {
                    RequestStreamedTextureDict(asset, false);
                }
            });
            while (menuTextureAssets.Any(asset => { return !HasStreamedTextureDictLoaded(asset); }))
            {
                await Delay(0);
            }
#endif
#if REDM
            menuTextureAssets.ForEach(asset =>
            {
                if (!Call<bool>(HAS_STREAMED_TEXTURE_DICT_LOADED, asset))
                {
                    Call(REQUEST_STREAMED_TEXTURE_DICT, asset, false);
                }
            });
            while (menuTextureAssets.Any(asset => { return !Call<bool>(HAS_STREAMED_TEXTURE_DICT_LOADED, asset); }))
            {
                await Delay(0);
            }
#endif
        }

        /// <summary>
        /// Unloads the texture dict for the common menu sprites.
        /// </summary>
        private static void UnloadAssets()
        {
#if FIVEM
            menuTextureAssets.ForEach(asset =>
            {
                if (HasStreamedTextureDictLoaded(asset))
                {
                    SetStreamedTextureDictAsNoLongerNeeded(asset);
                }
            });
#endif
#if REDM
            menuTextureAssets.ForEach(asset =>
            {
                if (Call<bool>(HAS_STREAMED_TEXTURE_DICT_LOADED, asset))
                {
                    Call(SET_STREAMED_TEXTURE_DICT_AS_NO_LONGER_NEEDED, asset);
                }
            });
#endif
        }

        /// <summary>
        /// Returns the currently opened menu.
        /// </summary>
        /// <returns></returns>
        public static Menu GetCurrentMenu()
        {
            if (Menus.Any((m) => m.Visible))
                return Menus.Find((m) => m.Visible);
            return null;
        }

        /// <summary>
        /// Returns true if any menu is currently open.
        /// </summary>
        /// <returns></returns>
        public static bool IsAnyMenuOpen() => Menus.Any((m) => m.Visible);


        #region Process Menu Buttons
        /// <summary>
        /// Process the select & go back/cancel buttons.
        /// </summary>
        /// <returns></returns>
        private async Task ProcessMainButtons()
        {
            if (IsAnyMenuOpen())
            {
#if REDM
                if (Call<bool>(IS_PAUSE_MENU_ACTIVE))
                {
                    return;
                }
#endif
                var currentMenu = GetCurrentMenu();
                if (currentMenu != null && !DontOpenAnyMenu)
                {
                    if (PreventExitingMenu)
                    {
#if FIVEM
                        Game.DisableControlThisFrame(0, Control.FrontendPause);
                        Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate);
#endif
#if REDM
                        Call(DISABLE_CONTROL_ACTION, 0, Control.FrontendPause, true);
                        Call(DISABLE_CONTROL_ACTION, 0, Control.FrontendPauseAlternate, true);
#endif
                    }

                    if (currentMenu.Visible && AreMenuButtonsEnabled)
                    {
                        // Select / Enter
                        if (
#if FIVEM
                            Game.IsDisabledControlJustReleased(0, Control.FrontendAccept) ||
                            Game.IsControlJustReleased(0, Control.FrontendAccept) ||
                            Game.IsDisabledControlJustReleased(0, Control.VehicleMouseControlOverride) ||
                            Game.IsControlJustReleased(0, Control.VehicleMouseControlOverride)
#endif
#if REDM
                            Call<bool>(IS_DISABLED_CONTROL_JUST_RELEASED, 0, Control.FrontendAccept) ||
                            Call<bool>(IS_CONTROL_JUST_RELEASED, 0, Control.FrontendAccept)
#endif
                            )
                        {
                            if (currentMenu.Size > 0)
                            {
                                currentMenu.SelectItem(currentMenu.CurrentIndex);
                            }
                        }
                        // Cancel / Go Back
                        else if (
#if FIVEM
                            Game.IsDisabledControlJustReleased(0, Control.PhoneCancel) 
#endif
#if REDM
                            Call<bool>(IS_DISABLED_CONTROL_JUST_RELEASED, 0, Control.FrontendCancel)
#endif
                            && !DisableBackButton)
                        {
                            // Wait for the next frame to make sure the "cinematic camera" button doesn't get "re-enabled" before the menu gets closed.
                            await Delay(0);
                            currentMenu.GoBack();
                        }
                        else if (
#if FIVEM
                            Game.IsDisabledControlJustReleased(0, Control.PhoneCancel) 
#endif
#if REDM
                               Call<bool>(IS_DISABLED_CONTROL_JUST_RELEASED, 0, Control.CellphoneCancel)
#endif
                            && PreventExitingMenu && !DisableBackButton)
                        {
                            // if there's a parent menu, allow going back to that, but don't allow a 'top-level' menu to be closed.
                            if (currentMenu.ParentMenu != null)
                            {
                                currentMenu.GoBack();
                            }
                            await Delay(0);
                        }
                    }
                }
#if FIVEM
                Game.DisableControlThisFrame(0, Control.MultiplayerInfo);
#endif
            }
        }

        /// <summary>
        /// Returns true when one of the 'up' controls is currently pressed, only if the button can be active according to some conditions.
        /// </summary>
        /// <returns></returns>
        private bool IsUpPressed()
        {
            // Return false if the buttons are not currently enabled.
            if (!AreMenuButtonsEnabled)
            {
                return false;
            }
#if FIVEM
            // when the player is holding TAB, while not in a vehicle, and when the scrollwheel is being used, return false to prevent interferring with weapon selection.
            if (!Game.PlayerPed.IsInVehicle())
            {
                if (Game.IsControlPressed(0, Control.SelectWeapon))
                {
                    if (Game.IsControlPressed(0, Control.SelectNextWeapon) || Game.IsControlPressed(0, Control.SelectPrevWeapon))
                    {
                        return false;
                    }
                }
            }

            // return true if the scrollwheel up or the arrow up key is being used at this frame.
            if (Game.IsControlPressed(0, Control.FrontendUp) ||
                Game.IsDisabledControlPressed(0, Control.FrontendUp) ||
                Game.IsControlPressed(0, Control.PhoneScrollBackward) ||
                Game.IsDisabledControlPressed(0, Control.PhoneScrollBackward))
            {
                return true;
            }
#endif
#if REDM
            if (Call<bool>(IS_CONTROL_PRESSED, 0, Control.FrontendUp) ||
                Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.FrontendUp) ||
                Call<bool>(IS_CONTROL_PRESSED, 0, Control.CellphoneScrollBackward) ||
                Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.CellphoneScrollBackward)
                )
            {
                return true;
            }
#endif
            // return false if none of the conditions matched.
            return false;
        }

        /// <summary>
        /// Returns true when one of the 'down' controls is currently pressed, only if the button can be active according to some conditions.
        /// </summary>
        /// <returns></returns>
        private bool IsDownPressed()
        {
            // Return false if the buttons are not currently enabled.
            if (!AreMenuButtonsEnabled)
            {
                return false;
            }
#if FIVEM
            // when the player is holding TAB, while not in a vehicle, and when the scrollwheel is being used, return false to prevent interferring with weapon selection.
            if (!Game.PlayerPed.IsInVehicle())
            {
                if (Game.IsControlPressed(0, Control.SelectWeapon))
                {
                    if (Game.IsControlPressed(0, Control.SelectNextWeapon) || Game.IsControlPressed(0, Control.SelectPrevWeapon))
                    {
                        return false;
                    }
                }
            }

            // return true if the scrollwheel down or the arrow down key is being used at this frame.
            if (Game.IsControlPressed(0, Control.FrontendDown) ||
                Game.IsDisabledControlPressed(0, Control.FrontendDown) ||
                Game.IsControlPressed(0, Control.PhoneScrollForward) ||
                Game.IsDisabledControlPressed(0, Control.PhoneScrollForward))
            {
                return true;
            }
#endif
#if REDM
            if (Call<bool>(IS_CONTROL_PRESSED, 0, Control.FrontendDown) ||
                Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.FrontendDown) ||
                Call<bool>(IS_CONTROL_PRESSED, 0, Control.CellphoneScrollForward) ||
                Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.CellphoneScrollForward)
                )
            {
                return true;
            }
#endif

            // return false if none of the conditions matched.
            return false;
        }

        /// <summary>
        /// Processes the menu toggle button to check if the menu should open or close.
        /// </summary>
        /// <returns></returns>
        private async Task ProcessToggleMenuButton()
        {

#if FIVEM
            Game.DisableControlThisFrame(0, MenuToggleKey);
            if (!Game.IsPaused && !IsPauseMenuRestarting() && IsScreenFadedIn() && !IsPlayerSwitchInProgress() && !Game.Player.IsDead && !DisableMenuButtons)
            {
                if (IsAnyMenuOpen())
                {
                    if (Game.CurrentInputMode == InputMode.MouseAndKeyboard)
                    {
                        if ((Game.IsControlJustPressed(0, MenuToggleKey) || Game.IsDisabledControlJustPressed(0, MenuToggleKey)) && !PreventExitingMenu)
                        {
                            var menu = GetCurrentMenu();
                            if (menu != null)
                            {
                                menu.CloseMenu();
                            }
                        }
                    }
                }
                else
                {
                    if (Game.CurrentInputMode == InputMode.GamePad)
                    {
                        if (!EnableMenuToggleKeyOnController)
                            return;

                        int tmpTimer = GetGameTimer();
                        while ((Game.IsControlPressed(0, Control.InteractionMenu) || Game.IsDisabledControlPressed(0, Control.InteractionMenu)) && !Game.IsPaused && IsScreenFadedIn() && !Game.Player.IsDead && !IsPlayerSwitchInProgress() && !DontOpenAnyMenu)
                        {
                            if (GetGameTimer() - tmpTimer > 400)
                            {
                                if (MainMenu != null)
                                {
                                    MainMenu.OpenMenu();
                                }
                                else
                                {
                                    if (Menus.Count > 0)
                                    {
                                        Menus[0].OpenMenu();
                                    }
                                }
                                break;
                            }
                            await Delay(0);
                        }
                    }
                    else
                    {
                        if ((Game.IsControlJustPressed(0, MenuToggleKey) || Game.IsDisabledControlJustPressed(0, MenuToggleKey)) && !Game.IsPaused && IsScreenFadedIn() && !Game.Player.IsDead && !IsPlayerSwitchInProgress() && !DontOpenAnyMenu)
                        {
                            if (Menus.Count > 0)
                            {
                                if (MainMenu != null)
                                {
                                    MainMenu.OpenMenu();
                                }
                                else
                                {
                                    Menus[0].OpenMenu();
                                }
                            }
                        }
                    }
                }
            }
#endif
#if REDM
            Call(DISABLE_CONTROL_ACTION, 0, MenuToggleKey, true);
            if (!Call<bool>(IS_PAUSE_MENU_ACTIVE) && Call<bool>(IS_SCREEN_FADED_IN) && !IsAnyMenuOpen() && !DisableMenuButtons && !Call<bool>(IS_ENTITY_DEAD, PlayerPedId()) && Call<bool>(IS_DISABLED_CONTROL_JUST_RELEASED, 0, MenuToggleKey))
            {
                MainMenu.OpenMenu();
            }
#endif
            await Task.FromResult(0);
        }

        /// <summary>
        /// Process left/right/up/down buttons (also holding down buttons will speed up after 3 iterations)
        /// </summary>
        /// <returns></returns>
        private async Task ProcessDirectionalButtons()
        {
            // Return if the buttons are not currently enabled.
            if (!AreMenuButtonsEnabled)
            {
                return;
            }

            // Get the currently open menu.
            var currentMenu = GetCurrentMenu();
            // If it exists.
            if (currentMenu != null && !DontOpenAnyMenu && currentMenu.Size > 0)
            {
                if (currentMenu.Visible)
                {
                    // Check if the Go Up controls are pressed.
                    if (IsUpPressed())
                    {
                        // Update the currently selected item to the new one.
                        currentMenu.GoUp();

                        // Get the current game time.
                        var time = GetGameTimer();
                        var times = 0;
                        var delay = 200;

                        // Do the following as long as the controls are being pressed.
                        while (IsUpPressed() && IsAnyMenuOpen() && GetCurrentMenu() != null)
                        {
                            // Update the current menu.
                            currentMenu = GetCurrentMenu();

                            // Check if the game time has changed by "delay" amount.
                            if (GetGameTimer() - time > delay)
                            {
                                // Increment the "changed indexes" counter
                                times++;

                                // If the controls are still being held down after moving 3 indexes, reduce the delay between index changes.
                                if (times > 2)
                                {
                                    delay = 150;
                                }
                                if (times > 5)
                                {
                                    delay = 100;
                                }
                                if (times > 25)
                                {
                                    delay = 50;
                                }
                                if (times > 60)
                                {
                                    delay = 25;
                                }

                                // Update the currently selected item to the new one.
                                currentMenu.GoUp();

                                // Reset the time to the current game timer.
                                time = GetGameTimer();
                            }

                            // Wait for the next game tick.
                            await Delay(0);
                        }
                    }

                    // Check if the Go Down controls are pressed.
                    else if (IsDownPressed())
                    {
                        currentMenu.GoDown();

                        var time = GetGameTimer();
                        var times = 0;
                        var delay = 200;
                        while (IsDownPressed() && GetCurrentMenu() != null)
                        {
                            currentMenu = GetCurrentMenu();
                            if (GetGameTimer() - time > delay)
                            {
                                times++;
                                if (times > 2)
                                {
                                    delay = 150;
                                }
                                if (times > 5)
                                {
                                    delay = 100;
                                }
                                if (times > 25)
                                {
                                    delay = 50;
                                }
                                if (times > 60)
                                {
                                    delay = 25;
                                }

                                currentMenu.GoDown();

                                time = GetGameTimer();
                            }
                            await Delay(0);
                        }
                    }

                    // Check if the Go Left controls are pressed.
#if FIVEM
                    else if (Game.IsDisabledControlJustPressed(0, Control.PhoneLeft) || Game.IsControlJustPressed(0, Control.PhoneLeft))
#endif
#if REDM
                    else if (Call<bool>(IS_DISABLED_CONTROL_JUST_PRESSED, 0, Control.FrontendLeft) || Call<bool>(IS_CONTROL_JUST_PRESSED, 0, Control.FrontendLeft))
#endif
                    {
                        var item = currentMenu.GetMenuItems()[currentMenu.CurrentIndex];
                        if (item.Enabled)
                        {
                            currentMenu.GoLeft();
                            var time = GetGameTimer();
                            var times = 0;
                            var delay = 200;
#if FIVEM
                            while ((Game.IsDisabledControlPressed(0, Control.PhoneLeft) || Game.IsControlPressed(0, Control.PhoneLeft)) && GetCurrentMenu() != null && AreMenuButtonsEnabled)
#endif
#if REDM
                            while ((Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.FrontendLeft) || Call<bool>(IS_CONTROL_PRESSED, 0, Control.FrontendLeft)) && GetCurrentMenu() != null && AreMenuButtonsEnabled)
#endif
                            {
                                currentMenu = GetCurrentMenu();
                                if (GetGameTimer() - time > delay)
                                {
                                    times++;
                                    if (times > 2)
                                    {
                                        delay = 150;
                                    }
                                    if (times > 5)
                                    {
                                        delay = 100;
                                    }
                                    if (times > 25)
                                    {
                                        delay = 50;
                                    }
                                    if (times > 60)
                                    {
                                        delay = 25;
                                    }
                                    currentMenu.GoLeft();
                                    time = GetGameTimer();
                                }
                                await Delay(0);
                            }
                        }
                    }

                    // Check if the Go Right controls are pressed.
#if FIVEM
                    else if (Game.IsDisabledControlJustPressed(0, Control.PhoneRight) || Game.IsControlJustPressed(0, Control.PhoneRight))
#endif
#if REDM
                    else if (AreMenuButtonsEnabled && Call<bool>(IS_DISABLED_CONTROL_JUST_PRESSED, 0, Control.FrontendRight) || Call<bool>(IS_CONTROL_JUST_PRESSED, 0, Control.FrontendRight))
#endif
                    {
                        var item = currentMenu.GetMenuItems()[currentMenu.CurrentIndex];
                        if (item.Enabled)
                        {
                            currentMenu.GoRight();
                            var time = GetGameTimer();
                            var times = 0;
                            var delay = 200;
#if FIVEM
                            while ((Game.IsDisabledControlPressed(0, Control.PhoneRight) || Game.IsControlPressed(0, Control.PhoneRight)) && GetCurrentMenu() != null && AreMenuButtonsEnabled)
#endif
#if REDM
                            while ((Call<bool>(IS_DISABLED_CONTROL_PRESSED, 0, Control.FrontendRight) || Call<bool>(IS_CONTROL_PRESSED, 0, Control.FrontendRight)) && GetCurrentMenu() != null && AreMenuButtonsEnabled)
#endif
                            {
                                currentMenu = GetCurrentMenu();
                                if (GetGameTimer() - time > delay)
                                {
                                    times++;
                                    if (times > 2)
                                    {
                                        delay = 150;
                                    }
                                    if (times > 5)
                                    {
                                        delay = 100;
                                    }
                                    if (times > 25)
                                    {
                                        delay = 50;
                                    }
                                    if (times > 60)
                                    {
                                        delay = 25;
                                    }
                                    currentMenu.GoRight();
                                    time = GetGameTimer();
                                }
                                await Delay(0);
                            }
                        }
                    }
                }
            }
        }

        private async Task MenuButtonsDisableChecks()
        {

            bool isInputVisible() => UpdateOnscreenKeyboard() == 0;
            if (isInputVisible())
            {
                bool buttonsState = DisableMenuButtons;
                while (isInputVisible())
                {
                    await Delay(0);
                    DisableMenuButtons = true;
                }
                int timer = GetGameTimer();
                while (GetGameTimer() - timer < 300)
                {
                    await Delay(0);
                    DisableMenuButtons = true;
                }
                DisableMenuButtons = buttonsState;
            }
        }
        #endregion

        /// <summary>
        /// Closes all menus.
        /// </summary>
        public static void CloseAllMenus()
        {
            Menus.ForEach((m) => { if (m.Visible) { m.CloseMenu(); } });
        }

        /// <summary>
        /// Disables the most important controls for when a menu is open.
        /// </summary>
        private static void DisableControls()
        {
            #region Disable Inputs when any menu is open.
            if (IsAnyMenuOpen())
            {
                var currMenu = GetCurrentMenu();
                if (currMenu != null)
                {
                    var currentItem = currMenu.GetCurrentMenuItem();
                    if (currentItem != null)
                    {
#if FIVEM
                        if (currentItem is MenuSliderItem || currentItem is MenuListItem || currentItem is MenuDynamicListItem)
                        {
                            if (Game.CurrentInputMode == InputMode.GamePad)
                                Game.DisableControlThisFrame(0, Control.SelectWeapon);
                        }
#endif
                    }

                    // Close all menus when the player dies.
#if FIVEM
                    if (Game.PlayerPed.IsDead)
#endif
#if REDM
                    if (Call<bool>(IS_ENTITY_DEAD, PlayerPedId()))
#endif
                    {
                        CloseAllMenus();
                    }

#if FIVEM
                    // Disable Gamepad/Controller Specific controls:
                    if (Game.CurrentInputMode == InputMode.GamePad)
                    {
                        Game.DisableControlThisFrame(0, Control.MultiplayerInfo);
                        // when in a vehicle.
                        if (Game.PlayerPed.IsInVehicle())
                        {
                            Game.DisableControlThisFrame(0, Control.VehicleHeadlight);
                            Game.DisableControlThisFrame(0, Control.VehicleDuck);

                            // toggles boost in some dlc vehicles, hence it's disabled for controllers only (pressing select in the menu would trigger this).
                            Game.DisableControlThisFrame(0, Control.VehicleFlyTransform);
                        }
                    }
                    else // when not using a controller.
                    {
                        Game.DisableControlThisFrame(0, Control.FrontendPauseAlternate); // disable the escape key opening the pause menu, pressing P still works.

                        // Disable the scrollwheel button changing weapons while the menu is open.
                        // Only if you press TAB (to show the weapon wheel) then it will allow you to change weapons.
                        if (!Game.IsControlPressed(0, Control.SelectWeapon))
                        {
                            Game.DisableControlThisFrame(24, Control.SelectNextWeapon);
                            Game.DisableControlThisFrame(24, Control.SelectPrevWeapon);
                        }
                    }
#endif
#if REDM
                    if (Call<bool>(_IS_INPUT_DISABLED, 2))
                    {
                        Call(DISABLE_CONTROL_ACTION, 0, Control.FrontendPauseAlternate, true);
                    }
#endif
                    // Disable Shared Controls

#if FIVEM
                    // Radio Inputs
                    Game.DisableControlThisFrame(0, Control.RadioWheelLeftRight);
                    Game.DisableControlThisFrame(0, Control.RadioWheelUpDown);
                    Game.DisableControlThisFrame(0, Control.VehicleNextRadio);
                    Game.DisableControlThisFrame(0, Control.VehicleRadioWheel);
                    Game.DisableControlThisFrame(0, Control.VehiclePrevRadio);

                    // Phone / Arrows Inputs
                    Game.DisableControlThisFrame(0, Control.Phone);
                    Game.DisableControlThisFrame(0, Control.PhoneCancel);
                    Game.DisableControlThisFrame(0, Control.PhoneDown);
                    Game.DisableControlThisFrame(0, Control.PhoneLeft);
                    Game.DisableControlThisFrame(0, Control.PhoneRight);

                    // Attack Controls
                    Game.DisableControlThisFrame(0, Control.Attack);
                    Game.DisableControlThisFrame(0, Control.Attack2);
                    Game.DisableControlThisFrame(0, Control.MeleeAttack1);
                    Game.DisableControlThisFrame(0, Control.MeleeAttack2);
                    Game.DisableControlThisFrame(0, Control.MeleeAttackAlternate);
                    Game.DisableControlThisFrame(0, Control.MeleeAttackHeavy);
                    Game.DisableControlThisFrame(0, Control.MeleeAttackLight);
                    Game.DisableControlThisFrame(0, Control.VehicleAttack);
                    Game.DisableControlThisFrame(0, Control.VehicleAttack2);
                    Game.DisableControlThisFrame(0, Control.VehicleFlyAttack);
                    Game.DisableControlThisFrame(0, Control.VehiclePassengerAttack);
                    Game.DisableControlThisFrame(0, Control.Aim);
                    Game.DisableControlThisFrame(0, Control.VehicleAim); // fires vehicle specific weapons when using right click on the mouse sometimes.

                    // When in a vehicle
                    if (Game.PlayerPed.IsInVehicle())
                    {
                        Game.DisableControlThisFrame(0, Control.VehicleSelectNextWeapon);
                        Game.DisableControlThisFrame(0, Control.VehicleSelectPrevWeapon);
                        Game.DisableControlThisFrame(0, Control.VehicleCinCam);
                    }
#endif
#if REDM
                    Call(DISABLE_CONTROL_ACTION, 0, Control.Attack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.Attack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.HorseAim, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.HorseAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.HorseAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.HorseMelee, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeBlock, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrapple, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleBreakout, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleChoke, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleMountSwitch, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleReversal, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeGrappleStandSwitch, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeHorseAttackPrimary, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeHorseAttackSecondary, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.MeleeModifier, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehBoatAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehBoatAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehCarAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehCarAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehDraftAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehDraftAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehFlyAttack, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehFlyAttack2, true);
                    Call(DISABLE_CONTROL_ACTION, 0, Control.VehPassengerAttack, true);
#endif
                }

            }
            #endregion
        }

        /// <summary>
        /// Draws all the menus that are visible on the screen.
        /// </summary>
        /// <returns></returns>
        private static async Task ProcessMenus()
        {

            if (Menus.Count > 0 &&
                IsAnyMenuOpen() &&
#if FIVEM
                IsScreenFadedIn() &&
                !Game.IsPaused &&
                !Game.Player.IsDead &&
                !IsPlayerSwitchInProgress()
#endif
#if REDM
                Call<bool>(IS_SCREEN_FADED_IN) &&
                !Call<bool>(IS_PAUSE_MENU_ACTIVE) &&
                !Call<bool>(IS_ENTITY_DEAD, PlayerPedId())
#endif
                )
            {
                await LoadAssets();

                DisableControls();

                Menu menu = GetCurrentMenu();
                if (menu != null)
                {
                    if (DontOpenAnyMenu)
                    {
                        if (menu.Visible && !menu.IgnoreDontOpenMenus)
                        {
                            menu.CloseMenu();
                        }
                    }
                    else if (menu.Visible)
                    {
                        menu.Draw();
                    }
                }

                if (EnableManualGCs)
                {
                    // once a minute
                    if (GetGameTimer() - ManualTimerForGC > 60000)
                    {
                        GC.Collect();
                        ManualTimerForGC = GetGameTimer();
                    }
                }
            }
            else
            {
                UnloadAssets();
            }
        }

#if FIVEM
        internal static async Task DrawInstructionalButtons()
        {
            if (!Game.IsPaused && !Game.Player.IsDead && IsScreenFadedIn() && !IsPlayerSwitchInProgress() && !IsWarningMessageActive() && UpdateOnscreenKeyboard() != 0)
            {
                Menu menu = GetCurrentMenu();
                if (menu != null && menu.Visible && menu.EnableInstructionalButtons)
                {
                    if (!HasScaleformMovieLoaded(_scale))
                    {
                        _scale = RequestScaleformMovie("INSTRUCTIONAL_BUTTONS");
                    }
                    while (!HasScaleformMovieLoaded(_scale))
                    {
                        await Delay(0);
                    }

                    BeginScaleformMovieMethod(_scale, "CLEAR_ALL");
                    EndScaleformMovieMethod();




                    for (int i = 0; i < menu.InstructionalButtons.Count; i++)
                    {
                        string text = menu.InstructionalButtons.ElementAt(i).Value;
                        Control control = menu.InstructionalButtons.ElementAt(i).Key;

                        BeginScaleformMovieMethod(_scale, "SET_DATA_SLOT");
                        ScaleformMovieMethodAddParamInt(i);
                        string buttonName = GetControlInstructionalButton(0, (int)control, 1);
                        PushScaleformMovieMethodParameterString(buttonName);
                        PushScaleformMovieMethodParameterString(text);
                        EndScaleformMovieMethod();
                    }

                    // Use custom instructional buttons FIRST if they're present.
                    if (menu.CustomInstructionalButtons.Count > 0)
                    {
                        for (int i = 0; i < menu.CustomInstructionalButtons.Count; i++)
                        {
                            Menu.InstructionalButton button = menu.CustomInstructionalButtons[i];
                            BeginScaleformMovieMethod(_scale, "SET_DATA_SLOT");
                            ScaleformMovieMethodAddParamInt(i + menu.InstructionalButtons.Count);
                            PushScaleformMovieMethodParameterString(button.controlString);
                            PushScaleformMovieMethodParameterString(button.instructionText);
                            EndScaleformMovieMethod();
                        }
                    }

                    BeginScaleformMovieMethod(_scale, "DRAW_INSTRUCTIONAL_BUTTONS");
                    ScaleformMovieMethodAddParamInt(0);
                    EndScaleformMovieMethod();

                    DrawScaleformMovieFullscreen(_scale, 255, 255, 255, 255, 0);
                    return;
                }
            }
            DisposeInstructionalButtonsScaleform();
        }
#endif

#if FIVEM
        private static void DisposeInstructionalButtonsScaleform()
        {
            if (HasScaleformMovieLoaded(_scale))
            {
                SetScaleformMovieAsNoLongerNeeded(ref _scale);
            }
        }
#endif
    }
}
