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
using System.Collections.Generic;
using System.Reflection;
using LeagueSharp.SDK.Core.Extensions.SharpDX;
using LeagueSharp.SDK.Core.Math.Prediction;
using SharpDX;
using Color = System.Drawing.Color;

namespace BadaoAIO.Orbwalker
{
    public static class Geometry
    {
        public static float Distance(this Vector2 point,
            Vector2 segmentStart,
            Vector2 segmentEnd,
            bool onlyIfOnSegment = false,
            bool squared = false)
        {
            var objects = point.ProjectOn(segmentStart, segmentEnd);

            if (objects.IsOnSegment || onlyIfOnSegment == false)
            {
                return squared
                    ? Vector2.DistanceSquared(objects.SegmentPoint, point)
                    : Vector2.Distance(objects.SegmentPoint, point);
            }
            return float.MaxValue;
        }
    }
    public static class Helper
    {


        public static List<Obj_AI_Hero> GetLhEnemiesNear(this Vector3 position, float range, float Healthpercent)
        {
            return GameObjects.EnemyHeroes.Where(hero => hero.IsValidTarget(range, true, position) && hero.HealthPercent <= Healthpercent).ToList();
        }

        public static bool UnderAllyTurret(this Vector3 Position)
        {
            return ObjectManager.Get<Obj_AI_Turret>().Any(t => t.IsAlly && !t.IsDead && t.IsValidTarget(950,false,Position));
        }


        public static int CountEnemiesInRange(float range)
        {
            return ObjectManager.Player.CountEnemiesInRange(range);
        }

        /// <summary>
        ///     Counts the enemies in range of Unit.
        /// </summary>
        public static int CountEnemiesInRange(this Obj_AI_Base unit, float range)
        {
            return unit.ServerPosition.CountEnemiesInRange(range);
        }

        /// <summary>
        ///     Counts the enemies in range of point.
        /// </summary>
        public static int CountEnemiesInRange(this Vector3 point, float range)
        {
            return GameObjects.EnemyHeroes.Count(h => h.IsValidTarget(range, true, point));
        }

        // Use same interface as CountEnemiesInRange
        /// <summary>
        ///     Count the allies in range of the Player.
        /// </summary>
        public static int CountAlliesInRange(float range)
        {
            return ObjectManager.Player.CountAlliesInRange(range);
        }

        /// <summary>
        ///     Counts the allies in range of the Unit.
        /// </summary>
        public static int CountAlliesInRange(this Obj_AI_Base unit, float range)
        {
            return unit.ServerPosition.CountAlliesInRange(range);
        }

        /// <summary>
        ///     Counts the allies in the range of the Point.
        /// </summary>
        public static int CountAlliesInRange(this Vector3 point, float range)
        {
            return GameObjects.AllyHeroes
                .Count(x => x.IsValidTarget(range, false, point));
        }

        public static List<Obj_AI_Hero> GetAlliesInRange(this Obj_AI_Base unit, float range)
        {
            return GetAlliesInRange(unit.ServerPosition, range);
        }

        public static List<Obj_AI_Hero> GetAlliesInRange(this Vector3 point, float range)
        {
            return
                GameObjects.AllyHeroes
                    .Where(x => point.DistanceSquared(x.ServerPosition) <= range * range).ToList();
        }

        public static List<Obj_AI_Hero> GetEnemiesInRange(this Obj_AI_Base unit, float range)
        {
            return GetEnemiesInRange(unit.ServerPosition, range);
        }

        public static List<Obj_AI_Hero> GetEnemiesInRange(this Vector3 point, float range)
        {
            return
                GameObjects.EnemyHeroes
                    .Where(x => point.DistanceSquared(x.ServerPosition) <= range * range).ToList();
        }

        /// <summary>
        ///     Returns true if the unit is under tower range.
        /// </summary>
        public static bool UnderTurret(this Obj_AI_Base unit)
        {
            return UnderTurret(unit.Position, true);
        }

        /// <summary>
        ///     Returns true if the unit is under turret range.
        /// </summary>
        public static bool UnderTurret(this Obj_AI_Base unit, bool enemyTurretsOnly)
        {
            return UnderTurret(unit.Position, enemyTurretsOnly);
        }

        public static bool UnderTurret(this Vector3 position, bool enemyTurretsOnly)
        {
            return
                ObjectManager.Get<Obj_AI_Turret>().Any(turret => turret.IsValidTarget(950, enemyTurretsOnly, position));
        }
    }
    public class CustomDamageIndicator
    {
        private const int BAR_WIDTH = 104;
        private const int LINE_THICKNESS = 9;

        private static LeagueSharp.SDK.Core.UI.DamageIndicator.DamageToUnitDelegate damageToUnit;

        private static readonly Vector2 BarOffset = new Vector2(10, 25);

        private static System.Drawing.Color _drawingColor;
        public static System.Drawing.Color DrawingColor
        {
            get { return _drawingColor; }
            set { _drawingColor = Color.FromArgb(170, value); }
        }

        public static bool Enabled { get; set; }

        public static void Initialize(LeagueSharp.SDK.Core.UI.DamageIndicator.DamageToUnitDelegate damageToUnit)
        {
            // Apply needed field delegate for damage calculation
            CustomDamageIndicator.damageToUnit = damageToUnit;
            DrawingColor = System.Drawing.Color.Green;
            Enabled = true;

            // Register event handlers
            Drawing.OnDraw += Drawing_OnDraw;
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Enabled)
            {
                foreach (var unit in GameObjects.EnemyHeroes.Where(u => u.IsValidTarget() && u.IsHPBarRendered))
                {
                    // Get damage to unit
                    var damage = damageToUnit(unit);

                    // Continue on 0 damage
                    if (damage <= 0)
                        continue;

                    // Get remaining HP after damage applied in percent and the current percent of health
                    var damagePercentage = ((unit.Health - damage) > 0 ? (unit.Health - damage) : 0) / unit.MaxHealth;
                    var currentHealthPercentage = unit.Health / unit.MaxHealth;

                    // Calculate start and end point of the bar indicator
                    var startPoint = new Vector2((int)(unit.HPBarPosition.X + BarOffset.X + damagePercentage * BAR_WIDTH), (int)(unit.HPBarPosition.Y + BarOffset.Y) - 5);
                    var endPoint = new Vector2((int)(unit.HPBarPosition.X + BarOffset.X + currentHealthPercentage * BAR_WIDTH) + 1, (int)(unit.HPBarPosition.Y + BarOffset.Y) - 5);

                    // Draw the line
                    Drawing.DrawLine(startPoint, endPoint, LINE_THICKNESS, DrawingColor);
                }
            }
        }
    }
}
