using System;
using System.Linq;
using System.Reflection.Emit;
using System.Windows.Forms;
using LeagueSharp;
using LeagueSharp.SDK.Core;
using LeagueSharp.SDK.Core.Enumerations;
using LeagueSharp.SDK.Core.Events;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.UI.IMenu.Values;
using LeagueSharp.SDK.Core.UI.INotifications;
using LeagueSharp.SDK.Core.Utils;
using LeagueSharp.SDK.Core.Wrappers;
using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;

namespace BadaoAIO
{
    public class Program 
    {
        public static Spell Q, Q2, W, W2, E, E2, R, R2;
        public static SpellSlot Smite, Ignite, Flash;
        public static Items.Item Bilgewater, BotRK, Youmuu, Tiamat, Hydra, Sheen, LichBane, IcebornGauntlet,TrinityForce, LudensEcho;
        public static Menu MainMenu;
        public static bool enabled = true;
        public static bool Enable
        {
            get
            {
                return enabled;
            }

            set
            {
                enabled = value;
                if (MainMenu != null)
                {
                    MainMenu["Enable"].GetValue<MenuBool>().Value = value;
                }
            }
        }
        public static Obj_AI_Hero Player
        {
            get { return ObjectManager.Player; }
        }
        private static void Main(string[] args)
        {
            if (args == null)
            {
                return;
            }
            Load.OnLoad += OnLoad;
        }
        private static void OnLoad(object sender, EventArgs e)
        {
            var plugin = Type.GetType("BadaoAIO.Plugin." + Player.ChampionName);
            if (plugin == null)
            {
                AddUI.Notif(Player.ChampionName + ": Not Supported !",10000);
                return;
            }
            AddUI.Notif(Player.ChampionName + ": Loaded !",10000);
            Bootstrap.Init(null);
            if (Player.ChampionName == "Rammus")
            {
                LeagueSharp.SDK.Core.Orbwalker.Enabled = false;
                Menu Orb = new Menu("Orbwalker", "Orbwalker", true).Attach();
                Orbwalker.Orbwalker.Initialize(Orb);
            }
            Bilgewater = new Items.Item(ItemId.Bilgewater_Cutlass, 550);
            BotRK = new Items.Item(ItemId.Blade_of_the_Ruined_King, 550);
            Youmuu = new Items.Item(ItemId.Youmuus_Ghostblade, 0);
            Tiamat = new Items.Item(ItemId.Tiamat_Melee_Only, 400);
            Hydra = new Items.Item(ItemId.Ravenous_Hydra_Melee_Only, 400);
            Sheen = new Items.Item(ItemId.Sheen,0);
            LichBane = new Items.Item(ItemId.Lich_Bane, 0);
            TrinityForce = new Items.Item(ItemId.Trinity_Force, 0);
            IcebornGauntlet = new Items.Item(ItemId.Iceborn_Gauntlet, 0);
            LudensEcho = new Items.Item(ItemId.Ludens_Echo, 0);

            foreach (var spell in
                Player.Spellbook.Spells.Where(
                    i =>
                        i.Name.ToLower().Contains("smite") &&
                        (i.Slot == SpellSlot.Summoner1 || i.Slot == SpellSlot.Summoner2)))
            {
                Smite = spell.Slot;
            }
            Ignite = Player.GetSpellSlot("summonerdot");
            Flash = Player.GetSpellSlot("summonerflash");

            MainMenu = new Menu("BadaoAIO", "BadaoAIO", true, Player.ChampionName);
            AddUI.Bool(MainMenu, "Enable", Player.ChampionName + " Enable", true);
            MainMenu.Attach();
            MainMenu.MenuValueChanged += MainMenu_MenuValueChanged;
            NewInstance(plugin);
        }

        private static void MainMenu_MenuValueChanged(object sender, LeagueSharp.SDK.Core.UI.IMenu.MenuValueChangedEventArgs e)
        {
            var boolean = sender as MenuBool;
            if (boolean != null)
            {
                if (boolean.Name.Equals("Enable"))
                {
                    enabled = boolean.Value;
                    if (boolean.Value)
                        AddUI.Notif(Player.ChampionName + ": Enabled !", 4000);
                    else
                        AddUI.Notif(Player.ChampionName + ": Disabled !", 4000);
                }
            }
        }


        private static void NewInstance(Type type)
        {
            var target = type.GetConstructor(Type.EmptyTypes);
            var dynamic = new DynamicMethod(string.Empty, type, new Type[0], target.DeclaringType);
            var il = dynamic.GetILGenerator();
            il.DeclareLocal(target.DeclaringType);
            il.Emit(OpCodes.Newobj, target);
            il.Emit(OpCodes.Stloc_0);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);
            ((Func<object>)dynamic.CreateDelegate(typeof(Func<object>)))();
        }

    }
    public class AddUI : Program
    {
        public static void Notif(string msg,float time)
        {
            var x = new Notification("BadaoAIO:  " + msg, "");
            Notifications.Add(x);
            DelayAction.Add(time,() => Notifications.Remove(x));
        }

        public static MenuSeparator Separator(Menu subMenu, string name, string display)
        {
            return subMenu.Add(new MenuSeparator(name, display));
        }

        public static MenuBool Bool(Menu subMenu, string name, string display, bool state = true)
        {
            return subMenu.Add(new MenuBool(name, display, state));
        }

        public static MenuKeyBind KeyBind(Menu subMenu,
            string name,
            string display,
            Keys key,
            KeyBindType type = KeyBindType.Press)
        {
            return subMenu.Add(new MenuKeyBind(name, display, key, type));
        }

        public static MenuList List(Menu subMenu, string name, string display, string[] array)
        {
            return subMenu.Add(new MenuList<string>(name, display, array));
        }

        public static MenuSlider Slider(Menu subMenu, string name, string display, int cur, int min = 0, int max = 100)
        {
            return subMenu.Add(new MenuSlider(name, display, cur, min, max));
        }
    }
}
