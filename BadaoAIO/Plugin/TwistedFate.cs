using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Windows.Forms;
using SharpDX;
using LeagueSharp;
using LeagueSharp.SDK.Core;
using LeagueSharp.SDK.Core.Math;
using LeagueSharp.SDK.Core.Enumerations;
using LeagueSharp.SDK.Core.Events;
using LeagueSharp.SDK.Core.Extensions;
using LeagueSharp.SDK.Core.Extensions.SharpDX;
using LeagueSharp.SDK.Core.UI.IMenu.Values;
using LeagueSharp.SDK.Core.UI;
using LeagueSharp.SDK.Core.UI.INotifications;
using LeagueSharp.SDK.Core.Utils;
using LeagueSharp.SDK.Core.Wrappers;
using BadaoAIO.Orbwalker;
using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;
using orbwalker = BadaoAIO.Orbwalker.Orbwalker;
using Orb = LeagueSharp.SDK.Core.Orbwalker;
using Color = System.Drawing.Color;

namespace BadaoAIO.Plugin
{
    internal class TwistedFate : AddUI
    {
        private static int cardtick, yellowtick;
        public static bool helpergold, helperblue, helperred;
        private static bool isobvious { get { return Variables.TickCount - cardtick <= 500; } }
        private static bool IsPickingCard { get { return Player.HasBuff("pickacard_tracker"); } }
        private static bool CanUseR2 { get { return R.IsReady() && Player.HasBuff("destiny_marker"); } }
        private static bool CanUseR1 { get { return R.IsReady() && !Player.HasBuff("destiny_marker"); } }
        private static bool PickACard { get { return W.Instance.Name == "PickACard"; } }
        private static bool GoldCard { get { return W.Instance.Name == "goldcardlock"; } }
        private static bool BlueCard { get { return W.Instance.Name == "bluecardlock"; } }
        private static bool RedCard { get { return W.Instance.Name == "redcardlock"; } }
        private static bool HasBlue { get { return Player.HasBuff("bluecardpreattack"); } }
        private static bool HasRed { get { return Player.HasBuff("redcardpreattack"); } }
        private static bool HasGold { get { return Player.HasBuff("goldcardpreattack"); } }
        private static string HasACard
        {
            get
            {
                if (Player.HasBuff("bluecardpreattack"))
                    return "blue";
                if (Player.HasBuff("goldcardpreattack"))
                    return "gold";
                if (Player.HasBuff("redcardpreattack"))
                    return "red";
                return "none";
            }
        }
        public TwistedFate()
        {
            Q = new Spell(SpellSlot.Q, 1400);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R);
            Q.SetSkillshot(0.25f, 40, 1000, false, SkillshotType.SkillshotLine);
            Q.DamageType = W.DamageType = E.DamageType = DamageType.Magical;
            Q.MinHitChance = HitChance.High;
            Menu Combo = new Menu("Combo", "Combo");
            {
                Bool(Combo, "Qc", "Q", true);
                Bool(Combo, "Qafterattackc", "Q after attack", true);
                Bool(Combo, "Qimmobilec", "Q on immobile", true);
                Slider(Combo, "Qhitc", "Q if will hit", 2, 1, 3);
                Bool(Combo, "Wc", "W", true);
                Bool(Combo, "pickgoldc", "Pick gold card while using R", true);
                Bool(Combo, "dontpickyellow1stc", "don't pick gold at 1st turn", false);
                MainMenu.Add(Combo);
            }
            Menu Harass = new Menu("Harass", "Harass");
            {
                Bool(Harass, "Qh", "Q", true);
                Bool(Harass, "Qafterattackh", "Q after attack", true);
                Bool(Harass, "Qimmobileh", "Q on immobile", true);
                Slider(Harass, "Qhith", "Q if will hit", 2, 1, 3);
                Bool(Harass, "Wh", "W", true);
                List(Harass, "Wcolorh", "W card type", new[] { "blue", "red", "gold" });
                Slider(Harass, "manah", "Min mana", 40, 0, 100);
                MainMenu.Add(Harass);
            }
            Menu Clear = new Menu("Clear", "Clear");
            {
                Bool(Clear, "Qj", "Q", true);
                Slider(Clear, "Qhitj", "Q if will hit", 2, 1, 3);
                Bool(Clear, "Wj", "W", true);
                List(Clear, "Wcolorj", "W card type", new[] { "blue", "red" });
                Slider(Clear, "wmanaj", "mana only W blue", 0, 0, 100);
                Slider(Clear, "manaj", "Min mana", 40, 0, 100);
                MainMenu.Add(Clear);
            }
            Menu Auto = new Menu("Auto", "Auto");
            {
                Bool(Auto, "throwyellowa", "gapclose + interrupt: throw gold card", true);
                Bool(Auto, "killsteala", "KillSteal Q", true);
                MainMenu.Add(Auto);
            }
            Menu Helper = new Menu("Helper", "Pick card Helper");
            {
                Bool(Helper, "enableh", "Enabale", true);
                KeyBind(Helper, "pickyellowh", "Pick Yellow", Keys.W,KeyBindType.Toggle);
                KeyBind(Helper, "pickblueh", "Pick Blue", Keys.G,KeyBindType.Toggle);
                KeyBind(Helper, "pickredh", "Pick Red", Keys.H,KeyBindType.Toggle);
                MainMenu.Add(Helper);
            }
            Menu drawMenu = new Menu("Draw", "Draw");
            {
                Bool(drawMenu, "Qd", "Q");
                Bool(drawMenu, "Rd", "R");
                Bool(drawMenu, "Hpd", "Damage Indicator");
                MainMenu.Add(drawMenu);
            }
            drawMenu.MenuValueChanged += drawMenu_MenuValueChanged;
            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            //Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            //GameObject.OnCreate += OnCreate;
            Gapcloser.OnGapCloser += Gapcloser_OnGapCloser;
            InterruptableSpell.OnInterruptableTarget += InterruptableSpell_OnInterruptableTarget;
            Drawing.OnEndScene += Drawing_OnEndScene;
            Orb.OnAction += Orbwalker_OnAction;
            DamageIndicator.DamageToUnit = TwistedFateDamage;
            CustomDamageIndicator.Initialize(TwistedFateDamage);
            DamageIndicator.Enabled = drawhp;
            CustomDamageIndicator.Enabled = drawhp;
        }

        private void drawMenu_MenuValueChanged(object sender, LeagueSharp.SDK.Core.UI.IMenu.MenuValueChangedEventArgs e)
        {
            if (!Enable) return;
            var boolean = sender as MenuBool;
            if (boolean != null)
            {
                if (boolean.Name.Equals("Hpd"))
                {
                    DamageIndicator.Enabled = boolean.Value;
                    CustomDamageIndicator.Enabled = boolean.Value;
                }
            }
        }

        private void InterruptableSpell_OnInterruptableTarget(object sender, InterruptableSpell.InterruptableTargetEventArgs e)
        {
            if (!Enable) 
                return;
            if (e.Sender.IsEnemy && e.Sender.InAutoAttackRange())
            {
                if (HasGold)
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, e.Sender);
                }
            }
        }

        private void Gapcloser_OnGapCloser(object sender, Gapcloser.GapCloserEventArgs e)
        {
            if (!Enable)
                return;
            if (e.Sender.IsEnemy && e.Sender.InAutoAttackRange())
            {
                if (HasGold)
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, e.Sender);
                }
            }
        }
        private static bool dontbeobvious { get { return MainMenu["Combo"]["dontpickyellow1stc"].GetValue<MenuBool>().Value; } }
        private static bool comboq { get { return MainMenu["Combo"]["Qc"].GetValue<MenuBool>().Value; } }
        private static bool comboqafterattack { get { return MainMenu["Combo"]["Qafterattackc"].GetValue<MenuBool>().Value; } }
        private static bool comboqimmobile { get { return MainMenu["Combo"]["Qimmobilec"].GetValue<MenuBool>().Value; } }
        private static int comboqhit { get { return MainMenu["Combo"]["Qhitc"].GetValue<MenuSlider>().Value; } }
        private static bool combow { get { return MainMenu["Combo"]["Wc"].GetValue<MenuBool>().Value; } }
        private static bool combopickgold { get { return MainMenu["Combo"]["pickgoldc"].GetValue<MenuBool>().Value; } }
        private static bool harassq { get { return MainMenu["Harass"]["Qh"].GetValue<MenuBool>().Value; } }
        private static bool harassqafterattack { get { return MainMenu["Harass"]["Qafterattackh"].GetValue<MenuBool>().Value; } }
        private static bool harassqimmobile { get { return MainMenu["Harass"]["Qimmobileh"].GetValue<MenuBool>().Value; } }
        private static int harassqhit { get { return MainMenu["Harass"]["Qhith"].GetValue<MenuSlider>().Value; } }
        private static bool harassw { get { return MainMenu["Harass"]["Wh"].GetValue<MenuBool>().Value; } }
        private static int harasswcolor { get { return MainMenu["Harass"]["Wcolorh"].GetValue<MenuList>().Index; } }
        private static int harassmana { get { return MainMenu["Harass"]["manah"].GetValue<MenuSlider>().Value; } }
        private static bool clearq { get { return MainMenu["Clear"]["Qj"].GetValue<MenuBool>().Value; } }
        private static int clearqhit { get { return MainMenu["Clear"]["Qhitj"].GetValue<MenuSlider>().Value; } }
        private static bool clearw { get { return MainMenu["Clear"]["Wj"].GetValue<MenuBool>().Value; } }
        private static int clearwcolor { get { return MainMenu["Clear"]["Wcolorj"].GetValue<MenuList>().Index; } }
        private static int clearwmana { get { return MainMenu["Clear"]["wmanaj"].GetValue<MenuSlider>().Value; } }
        private static int clearmana { get { return MainMenu["Clear"]["manaj"].GetValue<MenuSlider>().Value; } }
        private static bool autothrowyellow { get { return MainMenu["Auto"]["throwyellowa"].GetValue<MenuBool>().Value; } }
        private static bool autokillsteal { get { return MainMenu["Auto"]["killsteala"].GetValue<MenuBool>().Value; } }
        private static bool helperenable { get { return MainMenu["Helper"]["enableh"].GetValue<MenuBool>().Value; } }
        private static bool helperpickyellow { get { return MainMenu["Helper"]["pickyellowh"].GetValue<MenuKeyBind>().Active; } 
            set 
            {
                MainMenu["Helper"]["pickyellowh"].GetValue<MenuKeyBind>().Active = value;
            } }
        private static bool helperpickblue { get { return MainMenu["Helper"]["pickblueh"].GetValue<MenuKeyBind>().Active; } 
            set 
            {
                MainMenu["Helper"]["pickblueh"].GetValue<MenuKeyBind>().Active = value;
            } }
        private static bool helperpickred { get { return MainMenu["Helper"]["pickredh"].GetValue<MenuKeyBind>().Active; } 
            set 
            {
                MainMenu["Helper"]["pickredh"].GetValue<MenuKeyBind>().Active = value;
            } }
        private static bool drawq { get { return MainMenu["Draw"]["Qd"].GetValue<MenuBool>().Value; } }
        private static bool drawr { get { return MainMenu["Draw"]["Rd"].GetValue<MenuBool>().Value; } }
        private static bool drawhp { get { return MainMenu["Draw"]["Hpd"].GetValue<MenuBool>().Value; } }
        private static void AutoHelper()
        {
            if (autokillsteal && Q.IsReady())
            {
                foreach (
                    var x  in
                        GameObjects.Heroes.Where(
                            x =>
                                x.IsValidTarget(Q.Range) &&
                                Player.CalculateDamage(x, DamageType.Magical,
                                    new double[] {60, 110, 160, 210, 260}[Q.Level - 1]
                                    + 0.65*Player.FlatMagicDamageMod) > x.Health))
                {
                    Q.Cast(x);
                }
            }
            if (helperenable)
            {
                if(helperpickblue || helperpickred || helperpickyellow)
                {
                    //Player.IssueOrder(GameObjectOrder.MoveTo,Game.CursorPos);
                    if (!IsPickingCard && PickACard && Variables.TickCount - cardtick >= 500)
                    {
                        cardtick = Variables.TickCount;
                        W.Cast();
                    }
                    if (helperpickyellow && GoldCard) W.Cast();
                    if (helperpickblue && BlueCard) W.Cast();
                    if (helperpickred && RedCard) W.Cast();
                }
                if (HasGold)
                    helperpickyellow = false;
                if (HasBlue)
                    helperpickblue = false;
                if (HasRed)
                    helperpickred = false;
            }
            if (combow && Player.HasBuff("destiny_marker") && combopickgold && W.IsReady())
            {
                if (!IsPickingCard && PickACard && Variables.TickCount - cardtick >= 500)
                {
                    cardtick = Variables.TickCount;
                    W.Cast();
                }
                if (IsPickingCard && GoldCard)
                    W.Cast();
            }
        }
        private void Drawing_OnEndScene(EventArgs args)
        {
            if (!Enable)
                return;
            if (Player.IsDead)
                return;
            if (drawr)
            {
                Drawing.DrawCircle(Player.Position, 5500, Color.Aqua);
            }
        }
        private void OnDraw(EventArgs args)
        {
            if (!Enable)
                return;
            if (Player.IsDead)
                return;
            if (drawq)
                Drawing.DrawCircle(Player.Position, Q.Range, Color.Aqua);
        }

        private void Orbwalker_OnAction(object sender, LeagueSharp.SDK.Core.Orbwalker.OrbwalkerActionArgs e)
        {
            if (!Enable)
                return;
            if (e.Type == OrbwalkerType.AfterAttack && e.Target.IsValidTarget())
            {
                if (Variables.TickCount - yellowtick <= 1500) 
                    return;
                if (Orb.ActiveMode == OrbwalkerMode.Orbwalk && comboqafterattack)
                {
                    if (e.Target.IsValidTarget() && !e.Target.IsZombie)
                    {
                        var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                        if (target.IsValidTarget() && !target.IsZombie)
                        Q.Cast(Q.GetPrediction(target).CastPosition);
                    }
                }
                if (Orb.ActiveMode == OrbwalkerMode.Hybrid && harassqafterattack && Player.ManaPercent >= harassmana)
                {
                    if (e.Target.IsValidTarget() && !e.Target.IsZombie)
                    {
                        var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                        if (target.IsValidTarget() && !target.IsZombie)
                            Q.Cast(Q.GetPrediction(target).CastPosition);
                    }
                }
            }
            var mode = new OrbwalkerMode[] { OrbwalkerMode.Hybrid, OrbwalkerMode.Orbwalk };
            if (e.Type == OrbwalkerType.BeforeAttack)
            {
                if (IsPickingCard && mode.Contains(Orb.ActiveMode)) e.Process = false;
                else if (HasACard != "none" && !GameObjects.EnemyHeroes.Contains(e.Target) && Orb.ActiveMode == OrbwalkerMode.Hybrid)
                {
                    e.Process = false;
                    var target = TargetSelector.GetTarget(Player.GetRealAutoAttackRange(), DamageType.Magical);
                    if (target.IsValidTarget() && !target.IsZombie)
                        Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                }
                else if (HasACard != "none" && HasRed && Orb.ActiveMode == OrbwalkerMode.LaneClear)
                {
                    e.Process = false;
                    IDictionary<Obj_AI_Minion, int> creeps = new Dictionary<Obj_AI_Minion, int>();
                    foreach (var x in GameObjects.EnemyMinions.Where(x => x.InAutoAttackRange()))
                    {
                        creeps.Add(x, GameObjects.EnemyMinions.Count(y => y.IsValidTarget() && y.Distance(x.Position) <= 300));
                    }
                    foreach (var x in GameObjects.Jungle.Where(x => x.InAutoAttackRange()))
                    {
                        creeps.Add(x, GameObjects.Jungle.Count(y => y.IsValidTarget() && y.Distance(x.Position) <= 300));
                    }
                    var minion = creeps.OrderByDescending(x => x.Value).FirstOrDefault();
                    Player.IssueOrder(GameObjectOrder.AttackUnit, minion.Key);
                }
            }
            if (e.Type == OrbwalkerType.OnAttack)
            {
                if (HasGold)
                    yellowtick = Variables.TickCount;
            }
        }
        private void OnUpdate(EventArgs args)
        {
            if (!Enable)
            {
                DamageIndicator.Enabled = false;
                CustomDamageIndicator.Enabled = false;
                return;
            }
            AutoHelper();
            if (Orb.ActiveMode == OrbwalkerMode.Orbwalk)
                Combo();
            if (Orb.ActiveMode == OrbwalkerMode.Hybrid && Player.ManaPercent >= harassmana)
                Harass();
            if (Orb.ActiveMode == OrbwalkerMode.LaneClear )
                Clear();
        }
        private static void Combo()
        {
            if (Q.IsReady() && comboq)
            {
                if (comboqimmobile)
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget() && !x.IsZombie))
                        Q.CastIfHitchanceMinimum(x, HitChance.Immobile);
                }
                {
                    var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                    if (target.IsValidTarget() && !target.IsZombie)
                        Q.CastIfWillHit(target, comboqhit);
                }
            }
            if (combow && W.IsReady())
            {
                var target = TargetSelector.GetTarget(900, DamageType.Magical);
                if (target.IsValidTarget() && !target.IsZombie)
                {
                    if (!IsPickingCard && PickACard &&Variables.TickCount - cardtick >= 500)
                    {
                        cardtick = Variables.TickCount;
                        W.Cast();
                    }
                    if (IsPickingCard)
                    {
                        if (Player.Mana >= Q.Instance.ManaCost )
                        {
                            if (GoldCard && !(dontbeobvious && isobvious))
                            W.Cast();
                        }
                        else if (GameObjects.AllyHeroes.Any(x => x.IsValidTarget(800,false)))
                        {
                            if (GoldCard && !(dontbeobvious && isobvious))
                                W.Cast();
                        }
                        else if (BlueCard)
                        {
                            W.Cast();
                        }
                    }
                }
            }
        }
        private static void Harass()
        {
            if (Q.IsReady() && harassq && Player.ManaPercent >= clearmana)
            {
                if (harassqimmobile)
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget() && !x.IsZombie))
                        Q.CastIfHitchanceMinimum(x, HitChance.Immobile);
                }
                {
                    var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                    Q.CastIfWillHit(target, harassqhit);
                }
            }
            if (harassw && W.IsReady())
            {
                var target = TargetSelector.GetTarget(900, DamageType.Magical);
                if (target.IsValidTarget() && !target.IsZombie)
                {
                    if (!IsPickingCard && PickACard && Variables.TickCount - cardtick >= 500 && Player.ManaPercent >= clearmana)
                    {
                        cardtick = Variables.TickCount;
                        W.Cast();
                    }
                    if (IsPickingCard)
                    {
                        switch (harasswcolor)
                        {
                            case 0:
                                if (BlueCard)
                                    W.Cast();
                                break;
                            case 1:
                                if (RedCard)
                                    W.Cast();
                                break;
                            case 2:
                                if (GoldCard)
                                    W.Cast();
                                break;
                        }
                    }
                }
            }
        }
        private static void Clear()
        {
            if (Q.IsReady() && clearq && Player.ManaPercent >= clearmana)
            {
                var farm = Q.GetLineFarmLocation(Minion.GetMinionsPredictedPositions(ObjectManager.Get<Obj_AI_Base>()
                    .Where(
                        x =>
                            x.IsMinion && !(new GameObjectTeam[] {Player.Team, GameObjectTeam.Neutral}.Contains(x.Team)) &&
                            x.Distance(Player.Position) <= Q.Range).ToList()
                    , Q.Delay, Q.Width, Q.Speed, Player.Position, Q.Range, false, SkillshotType.SkillshotLine));
                if (farm.MinionsHit >= clearqhit)
                    Q.Cast(farm.Position);
            }
            if (W.IsReady() && clearw)
            {
                var target = GameObjects.Minions.Where(x => x.Team != Player.Team && x.IsValidTarget() && x.InAutoAttackRange());
                if (target.Any())
                {
                    if (!IsPickingCard && PickACard && Variables.TickCount - cardtick >= 500 && Player.ManaPercent >= clearmana)
                    {
                        cardtick = Variables.TickCount;
                        W.Cast();
                    }
                    if (IsPickingCard)
                    {
                        if (clearwmana > Player.Mana * 100 / Player.MaxMana)
                        {
                            if (BlueCard)
                                W.Cast();
                        }
                        else
                        {
                            switch (clearwcolor)
                            {
                                case 0:
                                    if (BlueCard)
                                        W.Cast();
                                    break;
                                case 1:
                                    if (RedCard)
                                        W.Cast();
                                    break;
                            }
                        }
                    }
                }
            }
        }
        private static float TwistedFateDamage(Obj_AI_Hero target)
        {
            var Qdamage = (float)Player.CalculateDamage(target,DamageType.Magical, new double[] { 60, 110, 160, 210, 260 }[Q.Level-1]
                                    + 0.65 * Player.FlatMagicDamageMod);
            var Wdamage = (float)Player.CalculateDamage(target, DamageType.Magical, new double[] { 40, 60, 80, 100, 120 }[W.Level - 1]
                                    + 1 * (Player.BaseAttackDamage + Player.FlatPhysicalDamageMod)
                                    + 0.5 * Player.FlatMagicDamageMod);
            float x = 0;
            if ((W.IsReady() || HasACard != "none") && Q.IsReady())
            {
                if ((Player.Mana >= Q.Instance.ManaCost + W.Instance.ManaCost) ||
                    (Player.Mana >= Q.Instance.ManaCost && HasACard != "none"))
                {
                    x = x + Qdamage + Wdamage;
                }
                else if (Player.Mana >= Q.Instance.ManaCost)
                {
                    x = x + Qdamage;
                }
                else if (Player.Mana >= W.Instance.ManaCost || HasACard != "none")
                {
                    x = x + Wdamage;
                }
            }
            else if (Q.IsReady())
            {
                x = x + Qdamage;
            }
            else if (W.IsReady() ||  HasACard != "none")
            {
                x = x + Wdamage;
            }
            if (LichBane.IsReady)
            {
                x = x +
                    (float)
                        Player.CalculateDamage(target, DamageType.Magical,
                            0.75*Player.BaseAttackDamage + 0.5*Player.FlatMagicDamageMod);
            }
            else if (TrinityForce.IsReady)
            {
                x = x + (float) Player.CalculateDamage(target, DamageType.Magical, 2*Player.BaseAttackDamage);
            }
            else if (IcebornGauntlet.IsReady)
            {
                x = x + (float) Player.CalculateDamage(target, DamageType.Magical, 1.25*Player.BaseAttackDamage);
            }
            else if (Sheen.IsReady)
            {
                x = x + (float) Player.CalculateDamage(target, DamageType.Magical, 1*Player.BaseAttackDamage);
            }
            if (LudensEcho.IsReady)
            {
                x = x + (float)Player.CalculateDamage(target, DamageType.Magical, 100 + 0.1 * Player.FlatMagicDamageMod);
            }
            x = x + (float)Player.GetAutoAttackDamage(target, true);
            return x;
        }
        private static void checkbuff()
        {
            var temp = Player.Buffs.Aggregate("", (current, buff) => current + ("( " + buff.Name + " , " + buff.Count + " )"));
            Game.PrintChat(temp);
        }
    }
}
