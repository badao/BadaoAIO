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
    internal class Ahri : AddUI
    {
        public static string ahri1 = "Ahri_Base_Orb_mis.troy";
        public static string ahri2 = "Ahri_Base_Orb_mis_02.troy";
        public static GameObject AhriOrbReturn { get { return ObjectManager.Get<GameObject>().FirstOrDefault(x => x.Name == "Ahri_Base_Orb_mis_02.troy"); } }
        public static GameObject AhriOrb { get { return ObjectManager.Get<GameObject>().FirstOrDefault(x => x.Name == "Ahri_Base_Orb_mis.troy"); } }
        public static List<Vector2> pos = new List<Vector2>();
        public static int Rcount;
        private static bool IsCombo { get { return Orb.ActiveMode == OrbwalkerMode.Orbwalk; } }
        private static bool IsHarass { get { return Orb.ActiveMode == OrbwalkerMode.Hybrid; } }
        private static bool IsClear { get { return Orb.ActiveMode == OrbwalkerMode.LaneClear; } }
        private static float Qdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                 new double[] { 40, 65, 90, 115, 140 }[Q.Level - 1]
                                    + 0.35 * Player.FlatMagicDamageMod) +
                   (float)Player.CalculateDamage(target, DamageType.True,
                 new double[] { 40, 65, 90, 115, 140 }[Q.Level - 1]
                                    + 0.35 * Player.FlatMagicDamageMod);
        }
        private static float Wdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                 new double[] { 40, 65, 90, 115, 140 }[W.Level - 1]
                                    + 0.4 * Player.FlatMagicDamageMod) * 1.6f;
        }
        private static float Edamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                new double[] { 60, 95, 130, 165, 200 }[E.Level - 1]
                                    + 0.50 * Player.FlatMagicDamageMod);
        }
        private static float Rdamage(Obj_AI_Base target)
        {
            return (float)Player.CalculateDamage(target, DamageType.Magical,
                                    (new double[] { 70, 110, 150 }[R.Level - 1]
                                    + 0.3 * Player.FlatMagicDamageMod) * 3);
        }

        private static float AhriDamage(Obj_AI_Hero target)
        {
            float x = 0;
            if (Player.Mana > Q.Instance.ManaCost)
            {
                if (Q.IsReady()) x += Qdamage(target);
                if (Player.Mana > Q.Instance.ManaCost + R.Instance.ManaCost)
                {
                    if (R.IsReady()) x += Rdamage(target);
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
            if (Ignite.IsReady())
            {
                x = x + (float)Player.GetSpellDamage(target, Ignite);
            }
            x = x + (float)Player.GetAutoAttackDamage(target, true);
            return x;
        }
        public Ahri()
        {
            Q = new Spell(SpellSlot.Q, 880);
            W = new Spell(SpellSlot.W, 550);
            E = new Spell(SpellSlot.E, 975);
            E2 = new Spell(SpellSlot.E, 975);
            R = new Spell(SpellSlot.R, 450);//600
            Q.SetSkillshot(0.25f, 100, 1600, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.25f, 60, 1550, true, SkillshotType.SkillshotLine);
            E2.SetSkillshot(0.25f, 60, 1550, true, SkillshotType.SkillshotLine);
            Q.DamageType = W.DamageType = E.DamageType = DamageType.Magical;
            Q.MinHitChance = HitChance.High;
            E.MinHitChance = HitChance.High;

            Menu Asassin = new Menu("Assassin", "AssasinMode");
            {
                KeyBind(Asassin, "activeAssasin", "Assassin Key", Keys.T, KeyBindType.Press);
                Separator(Asassin, "1", "Make sure you select a target");
                Separator(Asassin, "2", "before press this key");
                MainMenu.Add(Asassin);
            }
            Menu Combo = new Menu("Combo", "Combo");
            {
                Bool(Combo, "Qc", "Q", true);
                Bool(Combo, "Wc", "W", true);
                Bool(Combo, "Ec", "E", true);
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
            Menu Clear = new Menu("Clear", "Clear");
            {
                Bool(Clear, "Qj", "Q", true);
                Slider(Clear, "Qhitj", "Q if will hit", 2, 1, 3);
                Slider(Clear, "manaj", "Min mana", 40, 0, 100);
                MainMenu.Add(Clear);
            }
            Menu Auto = new Menu("Auto", "Auto");
            {
                KeyBind(Auto, "harassa", "Harass Q", Keys.H, KeyBindType.Toggle);
                Bool(Auto, "interrupta", "E interrupt + gapcloser", true);
                Bool(Auto, "killsteala", "KillSteal", true);
                MainMenu.Add(Auto);
            }
            Menu drawMenu = new Menu("Draw", "Draw");
            {
                Bool(drawMenu, "Qd", "Q");
                Bool(drawMenu, "Wd", "W");
                Bool(drawMenu, "Ed", "E");
                Bool(drawMenu, "Rd", "R");
                Bool(drawMenu, "Hpd", "Damage Indicator");
                MainMenu.Add(drawMenu);
            }
            drawMenu.MenuValueChanged += drawMenu_MenuValueChanged;

            Game.OnUpdate += Game_OnUpdate;
            Obj_AI_Base.OnNewPath += Obj_AI_Base_OnNewPath;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Gapcloser.OnGapCloser += Gapcloser_OnGapCloser;
            InterruptableSpell.OnInterruptableTarget += InterruptableSpell_OnInterruptableTarget;
            DamageIndicator.DamageToUnit = AhriDamage;
            CustomDamageIndicator.Initialize(AhriDamage);
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

        private static bool comboq { get { return MainMenu["Combo"]["Qc"].GetValue<MenuBool>().Value; } }
        private static bool combow { get { return MainMenu["Combo"]["Wc"].GetValue<MenuBool>().Value; } }
        private static bool comboe { get { return MainMenu["Combo"]["Ec"].GetValue<MenuBool>().Value; } }
        private static bool harassq { get { return MainMenu["Harass"]["Qh"].GetValue<MenuBool>().Value; } }
        private static bool harassw { get { return MainMenu["Harass"]["Wh"].GetValue<MenuBool>().Value; } }
        private static bool harasse { get { return MainMenu["Harass"]["Eh"].GetValue<MenuBool>().Value; } }
        private static int manaharass { get { return MainMenu["Harass"]["manah"].GetValue<MenuSlider>().Value; } }
        private static bool clearq { get { return MainMenu["Clear"]["Qj"].GetValue<MenuBool>().Value; } }
        private static int clearqhit { get { return MainMenu["Clear"]["Qhitj"].GetValue<MenuSlider>().Value; } }
        private static int manaclear { get { return MainMenu["Clear"]["manaj"].GetValue<MenuSlider>().Value; } }
        private static bool autoharassq { get { return MainMenu["Auto"]["harassa"].GetValue<MenuKeyBind>().Active; } }
        private static bool autointerrupt { get { return MainMenu["Auto"]["interrupta"].GetValue<MenuBool>().Value; } }
        private static bool autokillsteal { get { return MainMenu["Auto"]["killsteala"].GetValue<MenuBool>().Value; } }
        private static bool drawq { get { return MainMenu["Draw"]["Qd"].GetValue<MenuBool>().Value; } }
        private static bool draww { get { return MainMenu["Draw"]["Wd"].GetValue<MenuBool>().Value; } }
        private static bool drawe { get { return MainMenu["Draw"]["Ed"].GetValue<MenuBool>().Value; } }
        private static bool drawr { get { return MainMenu["Draw"]["Rd"].GetValue<MenuBool>().Value; } }
        private static bool drawhp { get { return MainMenu["Draw"]["Hpd"].GetValue<MenuBool>().Value; } }
        private static bool activeAssasin { get { return MainMenu["Assassin"]["activeAssasin"].GetValue<MenuKeyBind>().Active; } }

        private void InterruptableSpell_OnInterruptableTarget(object sender, InterruptableSpell.InterruptableTargetEventArgs e)
        {
            if (!Enable)
                return;
            if (e.Sender.IsEnemy && e.Sender.IsValidTarget(E.Range) && E.IsReady() && autointerrupt)
            {
                E.BadaoCast(e.Sender);
            }
        }

        private void Gapcloser_OnGapCloser(object sender, Gapcloser.GapCloserEventArgs e)
        {
            if (!Enable)
                return;
            if (e.Sender.IsEnemy && e.Sender.IsValidTarget(E.Range) && E.IsReady() && autointerrupt)
            {
                E.BadaoCast(e.Sender);
            }
        }
        private void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!Enable)
                return;
            if (sender.IsMe)
            {
                if (args.SData.Name == R.Instance.Name) Rcount = Variables.TickCount;
            }
            if (!activeAssasin && autoharassq && !sender.IsMe && sender.IsEnemy && (sender as Obj_AI_Hero).IsValidTarget(Q.Range) &&
                (AutoAttack.IsAutoAttack(args.SData.Name) || !args.SData.CanMoveWhileChanneling) && Player.ManaPercent >= manaharass)
            {
                Q.Cast(sender);
            }
        }

        private void Orbwalking_AfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (!Enable)
                return;
            // Q after attack
            if (!E.IsReady() && Q.IsReady() && (IsCombo || IsHarass))
            {
                foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(Q.Range)))
                    Q.CastIfWillHit(x, 2);
                if ((target as Obj_AI_Hero).IsValidTarget())
                    Q.Cast(target as Obj_AI_Hero);
            }
            // E after attack
            if (E.IsReady() && (IsCombo || IsHarass))
            {
                if ((target as Obj_AI_Hero).IsValidTarget())
                {
                    if (E.BadaoCast(target as Obj_AI_Hero))
                        DelayAction.Add(50, () => Q.Cast(target as Obj_AI_Hero));
                }
            }
        }

        private void Drawing_OnDraw(EventArgs args)
        {
            if (!Enable)
                return;
            if (Player.IsDead)
                return;
            if (drawq)
                Drawing.DrawCircle(Player.Position, Q.Range, Color.Aqua);
            if (draww)
                Drawing.DrawCircle(Player.Position, W.Range, Color.Aqua);
            if (drawe)
                Drawing.DrawCircle(Player.Position, E.Range, Color.Aqua);
            if (drawr)
                Drawing.DrawCircle(Player.Position, R.Range, Color.Aqua);
        }
        private void Obj_AI_Base_OnNewPath(Obj_AI_Base sender, GameObjectNewPathEventArgs args)
        {
            if (!Enable)
                return;
            if (Player.IsDashing()) return;
            var enemies = GameObjects.EnemyHeroes.Select(x => x.NetworkId).ToList();
            if (enemies.Contains(sender.NetworkId) && sender.IsValidTarget())
            {
                if (IsCombo)
                    DelayAction.Add(50, comboonnewpath);
                if (IsHarass && Player.ManaPercent >= manaharass)
                    DelayAction.Add(50, harassonnewpath);
            }
            if (activeAssasin)
            {
                var target = TargetSelector.GetSelectedTarget();
                if (target.IsValidTarget() && target.NetworkId == sender.NetworkId)
                {
                    AssasinOnNewPath();
                }
            }
        }
        private void Game_OnUpdate(EventArgs args)
        {
            if (!Enable)
            {
                DamageIndicator.Enabled = false;
                CustomDamageIndicator.Enabled = false;
                return;
            }
            if ((IsCombo || IsHarass) && Orb.CanMove
                && (Q.IsReady() || E.IsReady()))
            {
                Orb.Attack = false;
            }
            else Orb.Attack = true;
            if (autokillsteal && !activeAssasin)
                killstealUpdate();
            if (IsCombo)
                comboupdate();
            if (IsHarass && Player.ManaPercent >= manaharass)
                harassupdate();
            if (IsClear && Player.ManaPercent >= manaclear)
                ClearOnUpdate();
            if (activeAssasin)
                AssasinMode();
        }
        private static void killstealUpdate()
        {
            var enemies = GameObjects.EnemyHeroes.ToList();
            foreach (var x in enemies.Where(x => x.IsValidTarget(Q.Range) && Qdamage(x) > x.Health))
            {
                Q.Cast(x);
            }
            foreach (var x in enemies.Where(x => x.IsValidTarget(W.Range) && Wdamage(x) > x.Health))
            {
                W.Cast(x);
            }
            foreach (var x in enemies.Where(x => x.IsValidTarget(E.Range) && Edamage(x) > x.Health))
            {
                E.Cast(x);
            }
        }
        private static void comboupdate()
        {
            // use W
            if (combow)
            {
                var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                if (W.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    W.Cast();
                }
            }
            //use Q
            if (comboq)
            {
                if (Q.IsReady())
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(Q.Range) && !x.IsZombie))
                    {
                        if (x.HasBuffOfType(BuffType.Charm) || x.HasBuffOfType(BuffType.Stun) || x.HasBuffOfType(BuffType.Suppression))
                            if (Q.Cast(x) == CastStates.SuccessfullyCasted)
                                return;
                    }
                }
            }
        }
        private static void comboonnewpath()
        {
            // use Q
            if (comboq)
            {
                var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                if (Q.IsReady() && target.IsValidTarget() && !target.IsZombie &&
                    (!E.IsReady() || E.GetBadaoPrediction(target).CollisionObjects.Any()))
                {
                    if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                        return;
                }
                if (Q.IsReady() &&
                    (!E.IsReady() || E.GetBadaoPrediction(target).CollisionObjects.Any()))
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(Q.Range) && !x.IsZombie))
                    {
                        if (x.HasBuffOfType(BuffType.Charm) || x.HasBuffOfType(BuffType.Stun) || x.HasBuffOfType(BuffType.Suppression))
                            if (Q.Cast(x) == CastStates.SuccessfullyCasted)
                                return;
                    }
                }
                if (!comboe && Q.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                        return;
                }
            }
            //use E
            if (comboe)
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
                if (E.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    if (E.BadaoCast(target) && Q.IsReady() && comboq)
                    {
                        DelayAction.Add(50, () => Q.Cast(target));
                        return;
                    }
                }
                foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(E.Range) && !x.IsZombie))
                {
                    if (E.BadaoCast(x) && Q.IsReady() && comboq)
                    {
                        DelayAction.Add(50, () => Q.Cast(target));
                        return;
                    }
                }
            }

        }
        private static void harassupdate()
        {
            // use W
            if (harassw)
            {
                var target = TargetSelector.GetTarget(W.Range, DamageType.Magical);
                if (W.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    W.Cast();
                }
            }
            //use Q
            if (harassq)
            {
                if (Q.IsReady())
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(Q.Range) && !x.IsZombie))
                    {
                        if (x.HasBuffOfType(BuffType.Charm) || x.HasBuffOfType(BuffType.Stun) || x.HasBuffOfType(BuffType.Suppression))
                            if (Q.Cast(x) == CastStates.SuccessfullyCasted)
                                return;
                    }
                }
            }
        }
        private static void harassonnewpath()
        {
            // use Q
            if (harassq)
            {
                var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
                if (Q.IsReady() && target.IsValidTarget() && !target.IsZombie &&
                    (!E.IsReady() || E.GetBadaoPrediction(target).CollisionObjects.Any()))
                {
                    if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                        return;
                }
                if (Q.IsReady() &&
                    (!E.IsReady() || E.GetBadaoPrediction(target).CollisionObjects.Any()))
                {
                    foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(Q.Range) && !x.IsZombie))
                    {
                        if (x.HasBuffOfType(BuffType.Charm) || x.HasBuffOfType(BuffType.Stun) || x.HasBuffOfType(BuffType.Suppression))
                            if (Q.Cast(x) == CastStates.SuccessfullyCasted)
                                return;
                    }
                }
                if (!harasse && Q.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                        return;
                }
            }
            //use E
            if (harasse)
            {
                var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
                if (E.IsReady() && target.IsValidTarget() && !target.IsZombie)
                {
                    if (E.BadaoCast(target) && Q.IsReady() && harassq)
                    {
                        DelayAction.Add(50, () => Q.Cast(target));
                        return;
                    }
                }
                foreach (var x in GameObjects.EnemyHeroes.Where(x => x.IsValidTarget(E.Range) && !x.IsZombie))
                {
                    if (E.BadaoCast(x) && Q.IsReady() && harassq)
                    {
                        DelayAction.Add(50, () => Q.Cast(target));
                        return;
                    }
                }
            }

        }

        private static void ClearOnUpdate()
        {
            List<Vector2> minions =
                GameObjects.EnemyMinions.Where(x => x.IsValidTarget(Q.Range))
                    .Select(x => x.Position.ToVector2())
                    .ToList();
            var farmlocation = Q.GetLineFarmLocation(minions);
            if (clearq && Q.IsReady() && farmlocation.MinionsHit >= clearqhit)
                Q.Cast(farmlocation.Position);
        }

        private static void AssasinMode()
        {
            var target = TargetSelector.GetSelectedTarget();
            if (Orb.CanMove)
                Player.IssueOrder(GameObjectOrder.MoveTo, Game.CursorPos);
            if (target.IsValidTarget() && !target.IsZombie)
            {
                var targetpos = Movement.GetPrediction(target, 0.25f).UnitPosition.ToVector2();
                var distance = targetpos.Distance(Player.Position.ToVector2());
                if (Ignite.IsReady() && target.IsValidTarget(450))
                {
                    Player.Spellbook.CastSpell(Ignite, target);
                }
                if (!R.IsReady(3000) || Player.IsDashing())
                {
                    if (W.IsReady() && Player.Distance(target.Position) <= W.Range)
                    {
                        W.Cast();
                    }
                }
                if (R.IsReady() && AhriOrbReturn == null && AhriOrb == null && Variables.TickCount - Rcount >= 500)
                {
                    Vector2 intersec = new Vector2();
                    for (int i = 450; i >= 0; i = i - 50)
                    {
                        for (int j = 50; j <= 600; j = j + 50)
                        {
                            var vectors =
                                BadaoAIO.Orbwalker.Geometry.CircleCircleIntersection(Player.Position.ToVector2(),
                                    targetpos, i, j);
                            foreach (var x in vectors)
                            {
                                if (!Collide(x, target) && !x.IsWall())
                                {
                                    intersec = x;
                                    goto ABC;
                                }
                            }
                        }
                    }
                    ABC:
                    if (intersec.IsValid())
                        R.Cast(intersec.ToVector3());
                }
                else if (R.IsReady() && AhriOrbReturn != null &&
                         Player.Distance(targetpos) < Player.Distance(AhriOrbReturn.Position.ToVector2()) &&
                         Variables.TickCount - Rcount >= 0)
                {
                    var OrbPosition = AhriOrbReturn.Position.ToVector2();
                    var dis = OrbPosition.Distance(targetpos);
                    Vector2 castpos = new Vector2();
                    for (int i = 450; i >= 200; i = i - 50)
                    {
                        if (OrbPosition.Extend(targetpos, dis + i).Distance(Player.Position.ToVector2()) <= R.Range &&
                            !OrbPosition.Extend(targetpos, dis + i).IsWall())
                        {
                            castpos = OrbPosition.Extend(targetpos, dis + i);
                            break;
                        }
                    }
                    if (castpos.IsValid())
                        R.Cast(castpos.ToVector3());
                }
                if (Orb.CanAttack && target.InAutoAttackRange())
                {
                    Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                    //Orbwalking.LastAACommandTick = Utils.GameTimeTickCount - 4;
                    Orb.Movement = false;
                    DelayAction.Add(Player.AttackCastDelay*900,() => Orb.Movement = true);
                }
            }
        }
        private static void AssasinOnNewPath()
        {
            // use Q
            {
                var target = TargetSelector.GetSelectedTarget();
                if (Q.IsReady() && target.IsValidTarget() && !target.IsZombie &&
                    (!E.IsReady() || E.GetBadaoPrediction(target).CollisionObjects.Any()))
                {
                    if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                    {
                        Rcount = Variables.TickCount;
                        return;
                    }
                }
            }
            //use E
            {
                var target = TargetSelector.GetSelectedTarget();
                if (E.IsReady() && target.IsValidTarget() && !target.IsZombie && Variables.TickCount >= Rcount + 400)
                {
                    if (E.BadaoCast(target) && Q.IsReady())
                    {
                        DelayAction.Add(50, () => castQ(target));
                        return;
                    }
                }
            }

        }
        private static bool Collide(Vector2 pos, Obj_AI_Hero target)
        {
            E2.UpdateSourcePosition(pos.ToVector3(), pos.ToVector3());
            return
                E2.GetBadaoPrediction(target).CollisionObjects.Any();
        }
        private static void castQ(Obj_AI_Hero target)
        {
            if (Q.Cast(target) == CastStates.SuccessfullyCasted)
                Rcount = Variables.TickCount;
        }
    }
}
