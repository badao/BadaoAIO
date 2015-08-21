using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime;
using System.Security.Cryptography.X509Certificates;
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
using LeagueSharp.SDK.Core.Math.Prediction; 
using BadaoAIO.Orbwalker;
using SharpDX.IO;
using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;
using orbwalker = BadaoAIO.Orbwalker.Orbwalker;
using Orb = LeagueSharp.SDK.Core.Orbwalker;
using Color = System.Drawing.Color;

namespace BadaoAIO.Plugin
{
      class SyndraOrbs
     {
         public int Key;
         public GameObject Value;
         public SyndraOrbs(int key, GameObject value)
         {
             Key = key;
             Value = value;
         }
    }
     class LineEQs
     {
         public GameObject Key;
         public Vector2 Value;
         public LineEQs(GameObject key, Vector2 value)
         {
             Key = key;
             Value = value;
         }
     }
     class StunableOrbs
     {
         public Obj_AI_Hero Key;
         public GameObject Value;
         public StunableOrbs(Obj_AI_Hero key, GameObject value)
         {
             Key = key;
             Value = value;
         }
     }
    internal class Syndra : AddUI
    {
        private static int qcount, wcount, ecount, spellcount, waitE, w1cast;
        private static List<SyndraOrbs> SyndraOrb = new List<SyndraOrbs>();
        private static List <Obj_AI_Minion> seed { get { return GameObjects.AllyMinions.Where(i => i.Name == "Seed").ToList(); } }
        private static GameObject Wobject()
        {
            return
                GameObjects.AllGameObjects.FirstOrDefault(
                    obj => obj.Name.Contains("Syndra_Base_W") && obj.Name.Contains("held") && obj.Name.Contains("02"));
        }
        private static GameObject PickableOrb 
        {
            get
            {
                var firstOrDefault = SyndraOrb
                    .FirstOrDefault(x => x.Value.Position.ToVector2().Distance(Player.Position.ToVector2()) <= 950);
                return firstOrDefault !=
                       null ? firstOrDefault.Value : null;
            }
        }
        private  static Obj_AI_Minion PickableMinion
        {
            get
            {
                return
                    GameObjects.EnemyMinions
                        .FirstOrDefault(
                            x => x.IsValid && x.Position.ToVector2().Distance(Player.Position.ToVector2()) <= 950);
            }
        }
        private static List<LineEQs> LineEQ
        {
            get
            {
                if (Wobject() == null)
                {
                    return
                        SyndraOrb.Where(x => x.Value.Position.ToVector2().Distance(Player.Position.ToVector2()) <= 700)
                            .Select(
                                x =>
                                    new LineEQs(x.Value,
                                        Player.Position.ToVector2().Extend(x.Value.Position.ToVector2(), 1100)))
                            .ToList();
                }
                {
                    return
                        SyndraOrb.Where(
                            x =>
                                x.Value.Position.ToVector2().Distance(Wobject().Position.ToVector2()) >= 20 &&
                                x.Value.Position.ToVector2().Distance(Player.Position.ToVector2()) <= 700)
                            .Select(
                                x =>
                                    new LineEQs(x.Value,
                                        Player.Position.ToVector2().Extend(x.Value.Position.ToVector2(), 1100)))
                            .ToList();
                }
            }
        }

        private static List<StunableOrbs> StunAbleOrb 
        {
            get
            {
                return (from orb in LineEQ
                    from target in GameObjects.EnemyHeroes.Where(a => a.IsValidTarget())
                    where
                        Movement.GetPrediction(target, Player.Distance(target)/1600)
                            .UnitPosition.ToVector2()
                            .Distance(orb.Key.Position.ToVector2().Extend(orb.Value, -200), orb.Value, true) <=
                        target.BoundingRadius + 70
                    select new StunableOrbs(target, orb.Key)).ToList();
            }
        }

        private static bool CanEQtarget(Obj_AI_Hero target)
        {
            var pred = E.GetPrediction(target);
            if (pred.Hitchance < HitChance.OutOfRange) return false;
            return Player.Position.ToVector2().Distance(pred.CastPosition) <= 1200;
        }

        private static Vector2 PositionEQtarget(Obj_AI_Hero target)
        {
            var pred1 = E.GetPrediction(target);
            var pred2 = Q.GetPrediction(target);
            if (pred2.Hitchance >= HitChance.Medium &&
                pred2.UnitPosition.ToVector2().Distance(Player.Position.ToVector2()) <= E.Range)
                return pred2.UnitPosition.ToVector2();
            return pred1.Hitchance >= HitChance.OutOfRange
                ? Player.Position.ToVector2().Extend(pred1.UnitPosition.ToVector2(), E.Range)
                : new Vector2();
        }

        private static float Qdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                (new double[] { 50, 95, 140, 185, 230 }[Q.Level - 1]
                    + 0.6 * Player.FlatMagicDamageMod)
                * ((Q.Level == 5 && target is Obj_AI_Hero) ? 1.15 : 1));
        }
        private static float Wdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                (new double[] { 80, 120, 160, 200, 240 }[W.Level - 1]
                                    + 0.7 * Player.FlatMagicDamageMod));
        }
        private static float Edamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                (new double[] { 70, 115, 160, 205, 250 }[E.Level - 1]
                                    + 0.4 * Player.FlatMagicDamageMod));
        }
        private static float Rdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                                    new double[] { 90, 135, 180 }[R.Level - 1]
                                    + 0.2 * Player.FlatMagicDamageMod);
        }

        private static float SyndraHalfDamage(Obj_AI_Hero target)
        {
            float x = 0;
            if (Player.Mana > Q.Instance.ManaCost)
            {
                if (Q.IsReady()) x += Qdamage(target);
                if (Player.Mana > Q.Instance.ManaCost)
                {
                    if (Player.Mana > Q.Instance.ManaCost + E.Instance.ManaCost)
                    {
                        if (E.IsReady()) x += Edamage(target);
                        if (Player.Mana > Q.Instance.ManaCost+ E.Instance.ManaCost + W.Instance.ManaCost)
                            if (W.IsReady()) x += Wdamage(target);
                    }
                }

            }
            if (LudensEcho.IsReady)
            {
                x = x + (float)Player.CalculateDamage(target, DamageType.Magical, 100 + 0.1 * Player.FlatMagicDamageMod);
            }
            return x;
        }
        private static float SyndraDamage(Obj_AI_Hero target)
        {
            float x = 0;
            if (Player.Mana > Q.Instance.ManaCost)
            {
                if (Q.IsReady()) x += Qdamage(target);
                if (Player.Mana > Q.Instance.ManaCost + R.Instance.ManaCost)
                {
                    if (R.IsReady()) x += Rdamage(target) * (SyndraOrb.Count + 1);
                    if (Player.Mana > Q.Instance.ManaCost + R.Instance.ManaCost + E.Instance.ManaCost)
                    {
                        if (E.IsReady()) x += Edamage(target);
                        if (Player.Mana > Q.Instance.ManaCost + R.Instance.ManaCost + E.Instance.ManaCost + W.Instance.ManaCost)
                            if (W.IsReady()) x += Wdamage(target);
                    }
                }

            }
            if (LudensEcho.IsReady)
            {
                x = x + (float)Player.CalculateDamage(target, DamageType.Magical, 100 + 0.1 * Player.FlatMagicDamageMod);
            }
            x = x + (float)Player.GetAutoAttackDamage(target, true);
            return x;
        }
        public Syndra()
        {
            Q = new Spell(SpellSlot.Q, 800);
            W = new Spell(SpellSlot.W, 1150);
            E = new Spell(SpellSlot.E, 700); //1100
            R = new Spell(SpellSlot.R,675);
            Q.SetSkillshot(0.5f, 10, float.MaxValue, false, SkillshotType.SkillshotCircle);
            W.SetSkillshot(0.25f, 10, 1450, false, SkillshotType.SkillshotCircle, Player.Position, Player.Position);
            E.SetSkillshot(0.5f, 10, 1600, false, SkillshotType.SkillshotCircle);
            Q.DamageType = W.DamageType = E.DamageType = DamageType.Magical;
            Q.MinHitChance = HitChance.Medium;
            W.MinHitChance = HitChance.Medium;
            Menu Combo = new Menu("Combo", "Combo");
            {
                Bool(Combo, "Qc", "Q", true);
                Bool(Combo, "Wc", "W", true);
                Bool(Combo, "Ec", "E", true);
                Bool(Combo, "QEc", "QE", true);
                Bool(Combo, "Rc", "R", true);
                Separator(Combo, "Rbc", "cast R target:");
                foreach (var hero in GameObjects.EnemyHeroes)
                {
                    Bool(Combo, hero.ChampionName + "c", hero.ChampionName, true);
                }
                MainMenu.Add(Combo);
            }
            Menu Harass = new Menu("Harass", "Harass");
            {
                Bool(Harass, "Qh", "Q", true);
                Bool(Harass, "Wh", "W", true);
                Bool(Harass, "Eh", "E", true);
                Slider(Harass, "manah", "Min mana", 40, 0, 100);
                MainMenu.Add(Harass);
            }
            Menu Auto = new Menu("Auto", "Auto");
            {
                Bool(Auto, "Qa", "Q on target AA + spellcast ", true);
                Bool(Auto, "GapIntera", "Anti-Gap & Interrupt", true);
                Bool(Auto, "killsteala", "KillSteal ", true);
                MainMenu.Add(Auto);
            }
            Menu Helper = new Menu("Helper", "Helper");
            {
                Bool(Helper, "enableh", "Enabale", true);
                KeyBind(Helper, "QEh", "QE to mouse", Keys.G, KeyBindType.Press);
                MainMenu.Add(Helper);
            }
            Menu drawMenu = new Menu("Draw", "Draw");
            {
                Bool(drawMenu, "Qd", "Q");
                Bool(drawMenu, "Wd", "W");
                Bool(drawMenu, "Ed", "E");
                Bool(drawMenu, "QEd", "QE");
                Bool(drawMenu, "Rd", "R");
                Bool(drawMenu, "Hpd", "Damage Indicator");
                MainMenu.Add(drawMenu);
            }
            drawMenu.MenuValueChanged += drawMenu_MenuValueChanged;

            Game.OnUpdate += OnUpdate;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            GameObject.OnCreate += OnCreate;
            GameObject.OnDelete += OnDelete;
            Gapcloser.OnGapCloser += Gapcloser_OnGapCloser;
            InterruptableSpell.OnInterruptableTarget += InterruptableSpell_OnInterruptableTarget;
            //Orb.OnAction += Orbwalker_OnAction;
            DamageIndicator.DamageToUnit = SyndraDamage;
            CustomDamageIndicator.Initialize(SyndraDamage);
            DamageIndicator.Enabled = drawhp;
            CustomDamageIndicator.Enabled = drawhp;
            Obj_AI_Base.OnLevelUp += Obj_AI_Base_OnLevelUp;
        }
        private static bool castRtarget(Obj_AI_Hero target)
        {
            return MainMenu["Combo"][target.ChampionName + "c"].GetValue<MenuBool>().Value ;
        }
        private static bool comboq { get { return MainMenu["Combo"]["Qc"].GetValue<MenuBool>().Value; } }
        private static bool combow { get { return MainMenu["Combo"]["Wc"].GetValue<MenuBool>().Value; } }
        private static bool comboe { get { return MainMenu["Combo"]["Ec"].GetValue<MenuBool>().Value; } }
        private static bool comboqe { get { return MainMenu["Combo"]["QEc"].GetValue<MenuBool>().Value; } }
        private static bool combor { get { return MainMenu["Combo"]["Rc"].GetValue<MenuBool>().Value; } }
        private static bool harassE { get { return MainMenu["Harass"]["Eh"].GetValue<MenuBool>().Value; } }
        private static bool harassq { get { return MainMenu["Harass"]["Qh"].GetValue<MenuBool>().Value; } }
        private static bool harassw { get { return MainMenu["Harass"]["Wh"].GetValue<MenuBool>().Value; } }
        private static bool autoq { get { return MainMenu["Auto"]["Qa"].GetValue<MenuBool>().Value; } }
        private static bool autogapinter { get { return MainMenu["Auto"]["GapIntera"].GetValue<MenuBool>().Value; } }
        private static bool autokillsteal { get { return MainMenu["Auto"]["killsteala"].GetValue<MenuBool>().Value; } }
        private static bool helperenable { get { return MainMenu["Helper"]["enableh"].GetValue<MenuBool>().Value; } }
        private static bool helperqe { get { return MainMenu["Helper"]["QEh"].GetValue<MenuKeyBind>().Active; } }
        private static bool drawq { get { return MainMenu["Draw"]["Qd"].GetValue<MenuBool>().Value; } }
        private static bool draww { get { return MainMenu["Draw"]["Wd"].GetValue<MenuBool>().Value; } }
        private static bool drawe { get { return MainMenu["Draw"]["Ed"].GetValue<MenuBool>().Value; } }
        private static bool drawqe { get { return MainMenu["Draw"]["QEd"].GetValue<MenuBool>().Value; } }
        private static bool drawr { get { return MainMenu["Draw"]["Rd"].GetValue<MenuBool>().Value; } }
        private static bool drawhp { get { return MainMenu["Draw"]["Hpd"].GetValue<MenuBool>().Value; } }
        private static int harassmana { get { return MainMenu["Harass"]["manah"].GetValue<MenuSlider>().Value; } }

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
        private void OnUpdate(EventArgs args)
        {
            if (!Enable)
            {
                DamageIndicator.Enabled = false;
                CustomDamageIndicator.Enabled = false;
                return;
            }
            helper();
            killsteal();
            if (Orb.ActiveMode == OrbwalkerMode.Orbwalk)
                Combo();
            if (Orb.ActiveMode == OrbwalkerMode.Hybrid && Player.ManaPercent >= harassmana)
                Harass();
        }

        private void helper()
        {
            if (!helperenable) return;
            if (!helperqe) return;
            if (Player.Mana <= Q.Instance.ManaCost + E.Instance.ManaCost || !(Q.IsReady() && E.IsReady())) return;
            Q.Cast(Player.Position.Extend(Game.CursorPos, E.Range - 200));
            DelayAction.Add(250, () => E.Cast(Player.Position.Extend(Game.CursorPos, E.Range - 200)));
        }
        private void Obj_AI_Base_OnLevelUp(Obj_AI_Base sender, EventArgs args)
        {
            if (!sender.IsMe) return;
            if (Player.Level == 16)
                R = new Spell(SpellSlot.R, 750);
        }
        private void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            //Game.PrintChat(args.SData.Name);
            if (sender.IsMe)
            {
                if (args.SData.Name.ToLower().Contains("syndraq")) qcount = Variables.TickCount;
                if (args.SData.Name.ToLower() == "syndraw") w1cast = Variables.TickCount;
                if (args.SData.Name.ToLower().Contains("syndrawcast")) wcount = Variables.TickCount;
                if (args.SData.Name.ToLower().Contains("syndrae")) ecount = Variables.TickCount;
                spellcount = Math.Max(qcount, Math.Max(ecount, wcount));
            }
            if (!(Orb.ActiveMode == OrbwalkerMode.Orbwalk || Orb.ActiveMode == OrbwalkerMode.Hybrid || autoq))return;   
            if (sender is Obj_AI_Hero && sender.IsEnemy &&
                (AutoAttack.IsAutoAttack(args.SData.Name) || !args.SData.CanMoveWhileChanneling) &&
                sender.IsValidTarget(Q.Range))
            {
                if (Q.IsReady())
                    Q.Cast(Q.GetPrediction(sender).UnitPosition.ToVector2());
            }
        }
        private void InterruptableSpell_OnInterruptableTarget(object sender, InterruptableSpell.InterruptableTargetEventArgs e)
        {
            if (!Enable) return;
            if (e.Sender.IsEnemy && E.IsReady() && autogapinter)
            {
                if (e.Sender.IsValidTarget(E.Range)) E.Cast(e.Sender.Position);
                if (StunAbleOrb.Any())
                {
                    var i = StunAbleOrb.First(x => x.Key.NetworkId == e.Sender.NetworkId);
                    if (i.Value != null)
                        E.Cast(i.Value.Position.ToVector2());
                }
            }
        }

        private void Gapcloser_OnGapCloser(object sender, Gapcloser.GapCloserEventArgs e)
        {
            if (!Enable) return;
            if (e.Sender.IsEnemy && E.IsReady() && autogapinter)
            {
                if (e.Sender.IsValidTarget(E.Range)) E.Cast(e.Sender.Position);
                if (StunAbleOrb.Any())
                {
                    var i = StunAbleOrb.First(x => x.Key.NetworkId == e.Sender.NetworkId);
                    if (i.Value != null)
                        E.Cast(i.Value.Position.ToVector2());
                }
            }
        }

        private static void killsteal()
        {
            // killstealQ
            if (Q.IsReady() && Variables.TickCount >= spellcount + 1000)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x => x.IsValidTarget(Q.Range) && !x.IsZombie && Qdamage(x) > x.Health))
                {
                    Q.Cast(Q.GetPrediction(target).UnitPosition.ToVector2());
                    spellcount = Variables.TickCount;
                }
            }
            // killstealW
            if (W.IsReady() && Variables.TickCount >= spellcount + 1000)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x => x.IsValidTarget(W.Range) && !x.IsZombie && Wdamage(x) > x.Health))
                {
                    if (W.Instance.Name == "SyndraW")
                    {
                        if (PickableOrb != null || PickableMinion != null)
                        {
                            W.Cast(PickableOrb != null
                                ? PickableOrb.Position.ToVector2()
                                : PickableMinion.Position.ToVector2());
                        }
                        DelayAction.Add(500, () =>
                        {
                            W.UpdateSourcePosition(Wobject().Position);
                            W.Cast(W.GetPrediction(target).UnitPosition.ToVector2());
                        });
                        spellcount = Variables.TickCount + 500;
                    }
                    else
                    {
                        if (Wobject() != null && Variables.TickCount >= w1cast + 500)
                        {
                            W.UpdateSourcePosition(Wobject().Position);
                            W.Cast(W.GetPrediction(target).UnitPosition.ToVector2());
                            spellcount = Variables.TickCount;
                        }
                    }
                }
            }
            //killstealE
            if (E.IsReady() && Variables.TickCount >= spellcount + 1000)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x => x.IsValidTarget(E.Range) && !x.IsZombie && Edamage(x) > x.Health))
                {
                    E.Cast(target.Position);
                    spellcount = Variables.TickCount;
                }
            }
            //killstealQW
            if (Q.IsReady() && W.IsReady() && Variables.TickCount >= spellcount + 1000)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x => x.IsValidTarget(Q.Range) && !x.IsZombie && Qdamage(x) + Wdamage(x) > x.Health))
                {
                    Q.Cast(Q.GetPrediction(target).UnitPosition.ToVector2());
                    if (W.Instance.Name == "SyndraW")
                    {
                        if (PickableOrb != null || PickableMinion != null)
                        {
                            DelayAction.Add(250, () => W.Cast(PickableOrb != null
                                ? PickableOrb.Position.ToVector2()
                                : PickableMinion.Position.ToVector2()));
                        }
                        DelayAction.Add(750, () =>
                        {
                            W.UpdateSourcePosition(Wobject().Position);
                            W.Cast(W.GetPrediction(target).UnitPosition.ToVector2());
                        });
                        spellcount = Variables.TickCount + 750;
                    }
                    else
                    {
                        if (Wobject() != null && Variables.TickCount >= w1cast + 500)
                        {
                            W.UpdateSourcePosition(Wobject().Position);
                            DelayAction.Add(250, () => W.Cast(W.GetPrediction(target).UnitPosition.ToVector2()));
                            spellcount = Variables.TickCount + 250;
                        }
                    }
                }
            }
            //killstealR
            if (R.IsReady() && Variables.TickCount >= spellcount + 1000)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x => castRtarget(x) && x.IsValidTarget(W.Range) && !x.IsZombie && Rdamage(x)*SyndraOrb.Count > x.Health))
                {
                    R.Cast(target);
                    spellcount = Variables.TickCount;
                }
            }

        }

        private static void Harass()
        {
            if (Variables.TickCount > ecount)
            {
                
                if (Q.IsReady() && harassq)
                {
                    var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                    if (target.IsValidTarget() && !target.IsZombie)
                    {
                        Q.Cast(Q.GetPrediction(target).UnitPosition.ToVector2());
                        ecount = Variables.TickCount + 100;
                    }
                }
                if (E.IsReady() && StunAbleOrb.Any() && Variables.TickCount >= wcount + 500 && harassE)
                {
                    var targetE = TargetSelector.GetTarget(E.Range, DamageType.Magical);
                    var Orb = StunAbleOrb.Any(x => x.Key == targetE)
                        ? StunAbleOrb.First(x => x.Key == targetE).Value
                        : StunAbleOrb.First().Value;
                    if (Orb != null)
                    {
                        E.Cast(Orb.Position.ToVector2());
                        ecount = Variables.TickCount + 100;
                    }
                }
                if (W.Instance.Name != "SyndraW" && harassw)
                {
                    var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                    if (target.IsValidTarget() && !target.IsZombie)
                    {
                        if (Wobject() != null && Variables.TickCount >= w1cast + 250)
                        {
                            W.UpdateSourcePosition(Wobject().Position, Player.Position);
                            W.Cast(target);
                        }
                    }
                }
                if (W.IsReady() && Variables.TickCount >= ecount + 500 && harassw)
                {
                    var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                    if (target.IsValidTarget() && !target.IsZombie)
                    {
                        if (W.Instance.Name != "SyndraW")
                        {
                            if (Wobject() != null && Variables.TickCount >= w1cast + 250)
                            {
                                W.UpdateSourcePosition(Wobject().Position, Player.Position);
                                W.Cast(W.GetPrediction(target).UnitPosition.ToVector2());
                            }
                        }
                        else
                        {

                            if (PickableOrb != null || PickableMinion != null)
                            {
                                W.Cast(PickableOrb != null
                                    ? PickableOrb.Position.ToVector2()
                                    : PickableMinion.Position.ToVector2());
                                wcount = Variables.TickCount + 100;
                                ecount = Variables.TickCount + 100;
                            }
                        }
                    }
                }
                
            }
        }
        private static void Combo()
        {
            // Use R
            if (R.IsReady() && combor)
            {
                foreach (
                    var target in
                        GameObjects.EnemyHeroes.Where(
                            x =>
                                castRtarget(x) && x.IsValidTarget(W.Range) && !x.IsZombie && SyndraHalfDamage(x) < x.Health &&
                                SyndraDamage(x) > x.Health))
                {
                    R.Cast(target);
                }

            }
           
            // final cases;
            //else 
              if (Variables.TickCount> ecount)
            {
                {
                    if (R.IsReady() && E.IsReady() && combor && comboe)
                    {
                        var target =
                            GameObjects.EnemyHeroes.Where(x => castRtarget(x) && x.IsValidTarget() && !x.IsZombie)
                                .OrderByDescending(x => x.Distance(Player.Position))
                                .LastOrDefault();
                        if (target.IsValidTarget(R.Range) && !target.IsZombie)
                        {
                            var count = target.CountEnemiesInRange(400);
                            if (count >= 3)
                            {
                                R.Cast(target);
                                Q.Cast(Q.GetPrediction(target).UnitPosition.ToVector2());
                                DelayAction.Add(1000, () => E.Cast(target.Position));
                                ecount = Variables.TickCount + 1010;
                                return;
                            }
                        }
                    }
                }
                {
                    if (Q.IsReady() && comboq)
                    {
                        var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                        if (target.IsValidTarget() && !target.IsZombie)
                        {
                            var x = Q.GetPrediction(target).UnitPosition.ToVector2();
                            Q.Cast(x);
                            if (E.IsReady()
                                && x.Distance(Player.Position) <= E.Range - 100 && comboe)
                            {
                                DelayAction.Add(250, () => E.Cast(x));
                                ecount = Variables.TickCount + 350;
                            }
                        }
                    }
                    if (E.IsReady() && StunAbleOrb.Any() && Variables.TickCount >= wcount + 500 && comboe)
                    {
                        var targetE = TargetSelector.GetTarget(E.Range, DamageType.Magical);
                        var Orb = StunAbleOrb.Any(x => x.Key == targetE)
                            ? StunAbleOrb.First(x => x.Key == targetE).Value
                            : StunAbleOrb.First().Value;
                        if (Orb != null)
                        {
                            E.Cast(Orb.Position.ToVector2());
                            ecount = Variables.TickCount + 100;
                        }
                    }
                    if (W.Instance.Name != "SyndraW" && combow)
                    {
                        var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                        if (target.IsValidTarget() && !target.IsZombie)
                        {
                            if (Wobject() != null && Variables.TickCount >= w1cast + 250)
                            {
                                W.UpdateSourcePosition(Wobject().Position, Player.Position);
                                W.Cast(target);
                            }
                        }
                    }
                    if (W.IsReady() && Variables.TickCount >= ecount + 500 && combow)
                    {
                        var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                        if (target.IsValidTarget() && !target.IsZombie)
                        {
                            if (W.Instance.Name != "SyndraW")
                            {
                                if (Wobject() != null && Variables.TickCount >= w1cast + 250)
                                {
                                    W.UpdateSourcePosition(Wobject().Position,Player.Position);
                                    W.Cast(W.GetPrediction(target).UnitPosition.ToVector2());
                                }
                            }
                            else
                            {

                                if (PickableOrb != null || PickableMinion != null)
                                {
                                    W.Cast(PickableOrb != null
                                        ? PickableOrb.Position.ToVector2()
                                        : PickableMinion.Position.ToVector2());
                                    wcount = Variables.TickCount + 100;
                                    ecount = Variables.TickCount + 100;
                                }
                            }
                        }
                    }

                    if (Variables.TickCount > ecount && E.IsReady() && Q.IsReady() &&
                        Variables.TickCount >= wcount + 500 && comboqe &&
                        Player.Mana >= E.Instance.ManaCost + Q.Instance.ManaCost)
                    {
                        var target =
                            GameObjects.EnemyHeroes.FirstOrDefault(
                                x => x.IsValidTarget() && !x.IsZombie && CanEQtarget(x));
                        if (target.IsValidTarget() && !target.IsZombie)
                        {
                            var pos = PositionEQtarget(target);
                            if (pos.IsValid())
                            {
                                Q.Cast(pos);
                                if (pos.Distance(Player.Position.ToVector2()) < E.Range - 200)
                                {
                                    DelayAction.Add(250, () => E.Cast(pos));
                                    ecount = Variables.TickCount + 350;
                                }
                                else
                                {
                                    DelayAction.Add(150, () => E.Cast(pos));
                                    ecount = Variables.TickCount + 250;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void OnDelete(GameObject sender, EventArgs args)
        {
            if (!Enable) return;
            //if (sender.Name.Contains("idle"))
            //Game.PrintChat(sender.Name + " " + sender.Type);
            if (sender.Name.Contains("Syndra_Base_Q_idle.troy") || sender.Name.Contains("Syndra_Base_Q_Lv5_idle.troy"))
            {
                if (seed.Any(x => x.Position.ToVector2().Distance(sender.Position.ToVector2()) <= 20))
                {
                    //foreach (var x in SyndraOrb.Where(x => x.Key == sender.NetworkId))
                    //{
                    //    SyndraOrb.Remove(x);
                    //}
                    SyndraOrb.RemoveAll(x => x.Key == sender.NetworkId);
                }
            }
        }

        private void OnCreate(GameObject sender, EventArgs args)
        {
            if (!Enable) return;
            //Game.PrintChat(sender.Name + " " + sender.Type);
            if (sender.Name.Contains("Syndra_Base_Q_idle.troy") || sender.Name.Contains("Syndra_Base_Q_Lv5_idle.troy"))
            {
                if (seed.Any(x => x.Position.ToVector2().Distance(sender.Position.ToVector2()) <= 20))
                {
                    SyndraOrb.Add(new SyndraOrbs(sender.NetworkId,sender));
                }
            }

        }

        private void OnDraw(EventArgs args)
        {
            if (!Enable) return;
            //if (SyndraOrb.Any())
            //    foreach (var z in SyndraOrb)
            //    {
            //        Drawing.DrawCircle(z.Value.Position, 100, Color.Red);
            //    }
            //foreach (var y in x)
            //{
            //    Drawing.DrawCircle(y.Position, 200, Color.Red);
            //}
            //foreach (var obj in GameObjects.AllGameObjects)
            //{
            //    if (obj.Name.Contains("Syndra_Base_W") && obj.Name.Contains("held") && obj.Name.Contains("02"))
            //        Drawing.DrawCircle(obj.Position, 200, Color.Red);
            //}
            if (Player.IsDead)
                return;
            if (drawq)
                Drawing.DrawCircle(Player.Position, Q.Range, Color.Aqua);
            if (draww)
                Drawing.DrawCircle(Player.Position, W.Range, Color.Aqua);
            if (drawe)
                Drawing.DrawCircle(Player.Position, E.Range, Color.Aqua);
            if (drawqe)
                Drawing.DrawCircle(Player.Position, 1100 , Color.Aqua);
            if (drawr)
                Drawing.DrawCircle(Player.Position, R.Range, Color.Aqua);
        }


    }
}
