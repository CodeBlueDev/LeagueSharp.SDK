﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Orbwalker.cs" company="LeagueSharp">
//   Copyright (C) 2015 LeagueSharp
//   
//   This program is free software: you can redistribute it and/or modify
//   it under the terms of the GNU General Public License as published by
//   the Free Software Foundation, either version 3 of the License, or
//   (at your option) any later version.
//   
//   This program is distributed in the hope that it will be useful,
//   but WITHOUT ANY WARRANTY; without even the implied warranty of
//   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   GNU General Public License for more details.
//   
//   You should have received a copy of the GNU General Public License
//   along with this program.  If not, see <http://www.gnu.org/licenses/>.
// </copyright>
// <summary>
//   <c>Orbwalker</c> part that contains the internal system functionality of the <c>orbwalker</c>.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace LeagueSharp.SDK.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Windows.Forms;

    using LeagueSharp.SDK.Core.Enumerations;
    using LeagueSharp.SDK.Core.Events;
    using LeagueSharp.SDK.Core.Extensions;
    using LeagueSharp.SDK.Core.Extensions.SharpDX;
    using LeagueSharp.SDK.Core.Math.Prediction;
    using LeagueSharp.SDK.Core.UI.IMenu.Values;
    using LeagueSharp.SDK.Core.Utils;
    using LeagueSharp.SDK.Core.Wrappers;

    using SharpDX;

    using Color = System.Drawing.Color;
    using Menu = LeagueSharp.SDK.Core.UI.IMenu.Menu;

    /// <summary>
    ///     <c>Orbwalker</c> part that contains the internal system functionality of the <c>orbwalker</c>.
    /// </summary>
    public partial class Orbwalker
    {
        #region Static Fields

        /// <summary>
        ///     The attack time tracking list.
        /// </summary>
        private static readonly IDictionary<float, ActionArgs> AfterAttackTime = new Dictionary<float, ActionArgs>();

        /// <summary>
        ///     The local attack value.
        /// </summary>
        private static bool attack = true;

        /// <summary>
        ///     Indicates whether the missile was launched.
        /// </summary>
        private static bool isMissileLaunched = true;

        /// <summary>
        ///     The local last auto attack tick value.
        /// </summary>
        private static int lastAutoAttackTick;

        /// <summary>
        ///     The local last movement order tick value.
        /// </summary>
        private static int lastMovementOrderTick = Variables.TickCount;

        /// <summary>
        ///     The local movement value.
        /// </summary>
        private static bool movement = true;

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets or sets the active mode.
        /// </summary>
        public static OrbwalkerMode ActiveMode { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether attack.
        /// </summary>
        public static bool Attack
        {
            get
            {
                return attack
                       && Variables.TickCount + (Game.Ping / 2)
                       >= lastAutoAttackTick + (Player.BasicAttack.CastFrame * 1000)
                       + menu["advanced"]["miscExtraWindup"].GetValue<MenuSlider>().Value;
            }

            set
            {
                attack = value;
            }
        }

        /// <summary>
        ///     Gets or sets a value indicating whether movement.
        /// </summary>
        public static bool Movement
        {
            get
            {
                var flag = movement && !InterruptableSpell.IsCastingInterruptableSpell(Player, true);

                return (IsMissileLaunched
                        || (Player.CanCancelAutoAttack()
                                ? Variables.TickCount + (Game.Ping / 2)
                                  >= lastAutoAttackTick + (Player.AttackCastDelay * 1000) + 40
                                : Variables.TickCount - lastAutoAttackTick > 250)) && flag;
            }

            set
            {
                movement = value;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        ///     Gets or sets a value indicating whether is missile launched.
        /// </summary>
        private static bool IsMissileLaunched
        {
            get
            {
                return isMissileLaunched;
            }

            set
            {
                isMissileLaunched = value;
            }
        }

        /// <summary>
        ///     Gets the player.
        /// </summary>
        private static Obj_AI_Hero Player
        {
            get
            {
                return GameObjects.Player;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        ///     On create event.
        /// </summary>
        /// <param name="sender">
        ///     The sender
        /// </param>
        /// <param name="args">
        ///     The event data
        /// </param>
        private static void OnCreate(GameObject sender, EventArgs args)
        {
            var missile = sender as MissileClient;

            if (missile != null && missile.SpellCaster.IsMe && AutoAttack.IsAutoAttack(missile.SData.Name))
            {
                IsMissileLaunched = true;
            }
        }

        /// <summary>
        ///     On draw event.
        /// </summary>
        /// <param name="e">
        ///     The event data
        /// </param>
        private static void OnDraw(EventArgs e)
        {
            if (Player == null || !Player.IsValid)
            {
                return;
            }

            if (menu["drawings"]["drawAARange"].GetValue<MenuBool>().Value)
            {
                if (Player.Position.IsValid())
                {
                    Drawing.DrawCircle(Player.Position, Player.GetRealAutoAttackRange(), Color.Blue);
                }
            }

            if (menu["drawings"]["drawKillableMinion"].GetValue<MenuBool>().Value)
            {
                if (menu["drawings"]["drawKillableMinionFade"].GetValue<MenuBool>().Value)
                {
                    var minions =
                        GameObjects.EnemyMinions.Where(
                            m => m.IsValidTarget(1200F) && m.Health < Player.GetAutoAttackDamage(m, true) * 2);
                    foreach (var minion in minions)
                    {
                        var value = 255 - (minion.Health * 2);
                        value = value > 255 ? 255 : value < 0 ? 0 : value;

                        Drawing.DrawCircle(
                            minion.Position, 
                            minion.BoundingRadius * 2f, 
                            Color.FromArgb(255, 0, 255, (byte)(255 - value)));
                    }
                }
                else
                {
                    var minions =
                        GameObjects.EnemyMinions.Where(
                            m => m.IsValidTarget(1200F) && m.Health < Player.GetAutoAttackDamage(m, true));
                    foreach (var minion in minions)
                    {
                        Drawing.DrawCircle(minion.Position, minion.BoundingRadius * 2f, Color.FromArgb(255, 0, 255, 0));
                    }
                }
            }
        }

        /// <summary>
        ///     On process spell cast event.
        /// </summary>
        /// <param name="sender">
        ///     The sender
        /// </param>
        /// <param name="args">
        ///     The event data
        /// </param>
        private static void OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                var spellName = args.SData.Name;
                var target = args.Target as AttackableUnit;

                if (AutoAttack.IsAutoAttack(spellName))
                {
                    lastAutoAttackTick = Variables.TickCount - (Game.Ping / 2);
                    IsMissileLaunched = false;

                    var time = sender.AttackCastDelay * 1000 + 40;
                    var eventArgs = new ActionArgs
                                        {
                                            Target = target, Position = target != null ? target.Position : Player.Position, 
                                            Type = OrbwalkerType.AfterAttack
                                        };

                    if (!AfterAttackTime.ContainsKey(time))
                    {
                        AfterAttackTime.Add(time, eventArgs);
                    }

                    eventArgs = new ActionArgs
                                    {
                                        Target = target, Position = target != null ? target.Position : Player.Position, 
                                        Type = OrbwalkerType.OnAttack
                                    };
                    CallOnAction(eventArgs);
                }

                if (AutoAttack.IsAutoAttackReset(spellName))
                {
                    DelayAction.Add(250, () => { lastAutoAttackTick = 0; });
                }
            }
        }

        /// <summary>
        ///     On game update event.
        /// </summary>
        /// <param name="e">
        ///     The event data
        /// </param>
        private static void OnUpdate(EventArgs e)
        {
            if (menu == null)
            {
                return;
            }

            if (ActiveMode != OrbwalkerMode.None)
            {
                if (Attack)
                {
                    Preform(GetTarget(ActiveMode));
                    lastMovementOrderTick = Variables.TickCount;
                }

                if (Movement
                    && Variables.TickCount - lastMovementOrderTick
                    > menu["advanced"]["movementDelay"].GetValue<MenuSlider>().Value)
                {
                    MoveOrder(Game.CursorPos.SetZ());
                }
            }

            foreach (var item in AfterAttackTime.ToArray().Where(item => Variables.TickCount - item.Key >= 0))
            {
                CallOnAction(item.Value);
                AfterAttackTime.Remove(item);
            }
        }

        #endregion
    }

    /// <summary>
    ///     <c>Orbwalker</c> part that handles the events.
    /// </summary>
    public partial class Orbwalker
    {
        #region Delegates

        /// <summary>
        ///     The on action delegate.
        /// </summary>
        /// <param name="sender">
        ///     The sender
        /// </param>
        /// <param name="e">
        ///     The event data
        /// </param>
        public delegate void OnActionDelegate(object sender, ActionArgs e);

        #endregion

        #region Public Events

        /// <summary>
        ///     The on action event.
        /// </summary>
        public static event OnActionDelegate OnAction;

        #endregion

        #region Methods

        /// <summary>
        ///     Calls the OnAction event.
        /// </summary>
        /// <param name="e">
        ///     The event data
        /// </param>
        protected static void CallOnAction(ActionArgs e)
        {
            var handler = OnAction;
            if (handler != null)
            {
                handler(MethodBase.GetCurrentMethod().DeclaringType, e);
            }
        }

        #endregion
    }

    /// <summary>
    ///     The Action event data.
    /// </summary>
    public class ActionArgs : EventArgs
    {
        #region Public Properties

        /// <summary>
        ///     Gets the position.
        /// </summary>
        public Vector3 Position { get; internal set; }

        /// <summary>
        ///     Gets or sets a value indicating whether process.
        /// </summary>
        public bool Process { get; set; }

        /// <summary>
        ///     Gets the target.
        /// </summary>
        public AttackableUnit Target { get; internal set; }

        /// <summary>
        ///     Gets the type.
        /// </summary>
        public OrbwalkerType Type { get; internal set; }

        #endregion
    }

    /// <summary>
    ///     <c>Orbwalker</c> part that contains the internal modes functionality of the <c>orbwalker</c>.
    /// </summary>
    public partial class Orbwalker
    {
        #region Methods

        /// <summary>
        ///     Preforms the <c>orbwalker</c> action.
        /// </summary>
        /// <param name="target">
        ///     A target to attack.
        /// </param>
        private static void Preform(AttackableUnit target)
        {
            if (target != null && Player.IssueOrder(GameObjectOrder.AttackUnit, target))
            {
                var eventArgs = new ActionArgs
                                    {
                                        Target = target, Position = target.Position, Process = true, 
                                        Type = OrbwalkerType.BeforeAttack
                                    };
                CallOnAction(eventArgs);
            }
        }

        #endregion
    }

    /// <summary>
    ///     <c>Orbwalker</c> part that contains the internal menu functionality of the <c>orbwalker</c>.
    /// </summary>
    public partial class Orbwalker
    {
        #region Static Fields

        /// <summary>
        ///     The menu handle.
        /// </summary>
        private static Menu menu;

        #endregion

        #region Methods

        /// <summary>
        ///     Initializes the <c>Orbwalker</c>, starting from the menu.
        /// </summary>
        /// <param name="rootMenu">
        ///     The parent menu.
        /// </param>
        internal static void Initialize(Menu rootMenu)
        {
            menu = new Menu("orbwalker", "Orbwalker");

            menu.Add(new Menu("resetSpells", "Reset Spells"));
            menu.Add(new Menu("items", "Items"));

            var drawing = new Menu("drawings", "Drawings");
            drawing.Add(new MenuBool("drawAARange", "Draw Auto-Attack Range", true));
            drawing.Add(new MenuBool("drawTargetAARange", "Draw Target Auto-Attack Range"));
            drawing.Add(new MenuBool("drawKillableMinion", "Draw Killable Minion"));
            drawing.Add(new MenuBool("drawKillableMinionFade", "Enable Killable Minion Fade Effect"));
            menu.Add(drawing);

            var advanced = new Menu("advanced", "Advanced");
            advanced.Add(new MenuSeparator("separatorMovement", "Movement"));
            advanced.Add(
                new MenuSlider(
                    "movementDelay", 
                    "Delay between Movement", 
                    new Random(Variables.TickCount).Next(200, 301), 
                    0, 
                    2500));
            advanced.Add(new MenuBool("movementScramble", "Randomize movement location", true));
            advanced.Add(new MenuSlider("movementExtraHold", "Extra Hold Position", 25, 0, 250));
            advanced.Add(
                new MenuSlider(
                    "movementMaximumDistance", 
                    "Maximum Movement Distance", 
                    new Random().Next(500, 1201), 
                    0, 
                    1200));
            advanced.Add(new MenuSeparator("separatorMisc", "Miscellaneous"));
            advanced.Add(new MenuSlider("miscExtraWindup", "Extra Windup", 80, 0, 200));
            advanced.Add(new MenuSlider("miscFarmDelay", "Farm Delay", 0, 0, 200));
            advanced.Add(new MenuSeparator("separatorOther", "Other"));
            advanced.Add(new MenuButton("resetAll", "Settings", "Reset All Settings") { Action = ResetSettings });
            menu.Add(advanced);

            menu.Add(new MenuSeparator("separatorKeys", "Key Bindings"));
            menu.Add(new MenuKeyBind("lasthitKey", "Farm", Keys.X, KeyBindType.Press));
            menu.Add(new MenuKeyBind("laneclearKey", "Lane Clear", Keys.V, KeyBindType.Press));
            menu.Add(new MenuKeyBind("hybridKey", "Hybrid", Keys.C, KeyBindType.Press));
            menu.Add(new MenuKeyBind("orbwalkKey", "Orbwalk", Keys.Space, KeyBindType.Press));

            menu.MenuValueChanged += (sender, args) =>
                {
                    var keyBind = sender as MenuKeyBind;
                    if (keyBind != null)
                    {
                        var modeName = keyBind.Name.Substring(0, keyBind.Name.IndexOf("Key", StringComparison.Ordinal));
                        OrbwalkerMode mode;
                        if (Enum.TryParse(modeName, true, out mode))
                        {
                            if (keyBind.Active)
                            {
                                ActiveMode = mode;
                            }
                            else
                            {
                                if (mode == ActiveMode)
                                {
                                    ActiveMode = OrbwalkerMode.None;
                                }
                            }
                        }
                    }
                };

            rootMenu.Add(menu);

            Game.OnUpdate += OnUpdate;
            GameObject.OnCreate += OnCreate;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpellCast;
            Drawing.OnDraw += OnDraw;
        }

        /// <summary>
        ///     Resets the <c>orbwalker</c> settings.
        /// </summary>
        private static void ResetSettings()
        {
            menu["advanced"]["movementScramble"].GetValue<MenuBool>().RestoreDefault();
            menu["advanced"]["movementExtraHold"].GetValue<MenuSlider>().RestoreDefault();
            menu["advanced"]["miscExtraWindup"].GetValue<MenuSlider>().RestoreDefault();
            menu["advanced"]["miscFarmDelay"].GetValue<MenuSlider>().RestoreDefault();
            menu["lasthitKey"].GetValue<MenuKeyBind>().RestoreDefault();
            menu["laneclearKey"].GetValue<MenuKeyBind>().RestoreDefault();
            menu["hybridKey"].GetValue<MenuKeyBind>().RestoreDefault();
            menu["orbwalkKey"].GetValue<MenuKeyBind>().RestoreDefault();
            menu["advanced"]["movementMaximumDistance"].GetValue<MenuSlider>().Value = new Random().Next(500, 1201);
            menu["advanced"]["movementDelay"].GetValue<MenuSlider>().Value = new Random().Next(200, 301);
        }

        #endregion
    }

    /// <summary>
    ///     <c>Orbwalker</c> part that contains the external API functionality of the <c>orbwalker</c>.
    /// </summary>
    public partial class Orbwalker
    {
        #region Public Methods and Operators

        /// <summary>
        ///     Returns a target based on the <c>OrbwalkerMode</c>.
        /// </summary>
        /// <param name="mode">
        ///     The <c>OrbwalkerMode</c>
        /// </param>
        /// <returns>
        ///     The target in a <c>AttackableUnit</c> instance
        /// </returns>
        public static AttackableUnit GetTarget(OrbwalkerMode mode = OrbwalkerMode.None)
        {
            switch (mode)
            {
                case OrbwalkerMode.None:
                case OrbwalkerMode.Orbwalk:
                    {
                        return TargetSelector.GetTarget(-1f);
                    }

                default:
                    {
                        var target = TargetSelector.GetTarget(-1f);
                        if (mode == OrbwalkerMode.Hybrid && target != null)
                        {
                            return target;
                        }

                        var minionList =
                            GameObjects.EnemyMinions.Where(m => m.IsValidTarget(m.GetRealAutoAttackRange())).ToList();
                        foreach (var minion in minionList)
                        {
                            var time =
                                (int)
                                (Player.BasicAttack.CastFrame * 1000
                                 + (Player.Distance(minion) / Player.GetProjectileSpeed() * 1000) + Game.Ping / 2f);
                            var prediction = Health.GetPrediction(minion, time, 0);

                            if (prediction <= 0)
                            {
                                var eventArgs = new ActionArgs
                                                    {
                                                        Target = minion, Position = minion.Position, Process = true, 
                                                        Type = OrbwalkerType.NonKillableMinion
                                                    };
                                CallOnAction(eventArgs);
                            }
                            else if (prediction <= Player.GetAutoAttackDamage(minion))
                            {
                                return minion;
                            }
                        }

                        foreach (var turret in
                            from turret in
                                GameObjects.EnemyTurrets.Where(t => t.IsValidTarget(t.GetRealAutoAttackRange()))
                            let time =
                                (int)
                                (Player.BasicAttack.CastFrame * 1000
                                 + (Player.Distance(turret) / Player.GetProjectileSpeed() * 1000) + Game.Ping / 2f)
                            let predictedHealth = Health.GetPrediction(turret, time, 0)
                            where predictedHealth > 0 && predictedHealth <= Player.GetAutoAttackDamage(turret, true)
                            select turret)
                        {
                            return turret;
                        }

                        if (mode == OrbwalkerMode.LaneClear)
                        {
                            var shouldWait =
                                minionList.Any(
                                    m =>
                                    Health.GetPrediction(
                                        m, 
                                        (int)
                                        ((Player.AttackCastDelay * 1000) * 2
                                         + Player.Distance(m) / Player.GetProjectileSpeed() * 1000 + Game.Ping / 2f) * 2, 
                                        0) <= Player.GetAutoAttackDamage(m, true));

                            if (!shouldWait)
                            {
                                target = TargetSelector.GetTarget(-1f);
                                if (target != null)
                                {
                                    return target;
                                }

                                var minion = (from m in minionList
                                              let predictedHealth =
                                                  Health.GetPrediction(
                                                      m, 
                                                      (int)
                                                      ((Player.AttackCastDelay * 1000) * 2
                                                       + Player.Distance(m) / Player.GetProjectileSpeed() * 1000
                                                       + Game.Ping / 2f) * 2, 
                                                      0)
                                              where
                                                  predictedHealth >= 2 * Player.GetAutoAttackDamage(m, true)
                                                  || System.Math.Abs(predictedHealth - m.Health) < float.Epsilon
                                              select m).MaxOrDefault(m => m.Health);
                                if (minion != null)
                                {
                                    return minion;
                                }

                                var mob =
                                    (GameObjects.JungleLegendary.FirstOrDefault(
                                        j => j.IsValidTarget(j.GetRealAutoAttackRange()))
                                     ?? GameObjects.JungleSmall.FirstOrDefault(
                                         j =>
                                         j.IsValidTarget(j.GetRealAutoAttackRange()) && j.Name.Contains("Mini")
                                         && j.Name.Contains("SRU_Razorbeak"))
                                     ?? GameObjects.JungleLarge.FirstOrDefault(
                                         j => j.IsValidTarget(j.GetRealAutoAttackRange())))
                                    ?? GameObjects.JungleSmall.FirstOrDefault(
                                        j => j.IsValidTarget(j.GetRealAutoAttackRange()));
                                if (mob != null)
                                {
                                    return mob;
                                }

                                var turret =
                                    GameObjects.EnemyTurrets.FirstOrDefault(
                                        t => t.IsValidTarget(t.GetRealAutoAttackRange()));
                                if (turret != null)
                                {
                                    return turret;
                                }

                                var inhibitor =
                                    GameObjects.EnemyInhibitors.FirstOrDefault(
                                        i => i.IsValidTarget(i.GetRealAutoAttackRange()));
                                if (inhibitor != null)
                                {
                                    return inhibitor;
                                }

                                if (GameObjects.EnemyNexus != null
                                    && GameObjects.EnemyNexus.IsValidTarget(
                                        GameObjects.EnemyNexus.GetRealAutoAttackRange()))
                                {
                                    return GameObjects.EnemyNexus;
                                }
                            }
                        }

                        return null;
                    }
            }
        }

        /// <summary>
        ///     Orders a move onto the player, with the <c>orbwalker</c> parameters.
        /// </summary>
        /// <param name="position">
        ///     The position to <c>orbwalk</c> to.
        /// </param>
        public static void MoveOrder(Vector3 position)
        {
            if (position.Distance(Player.Position)
                > Player.BoundingRadius + menu["advanced"]["movementExtraHold"].GetValue<MenuSlider>().Value)
            {
                if (position.Distance(Player.Position)
                    > menu["advanced"]["movementMaximumDistance"].GetValue<MenuSlider>().Value)
                {
                    position = Player.Position.Extend(
                        position, 
                        menu["advanced"]["movementMaximumDistance"].GetValue<MenuSlider>().Value);
                }

                if (menu["advanced"]["movementScramble"].GetValue<MenuBool>().Value)
                {
                    var random = new Random(Variables.TickCount);
                    var angle = 2D * System.Math.PI * random.NextDouble();
                    var radius = Player.Distance(Game.CursorPos) < 360 ? 0F : Player.BoundingRadius * 2F;
                    position =
                        new Vector3(
                            (float)(position.X + radius * System.Math.Cos(angle)), 
                            (float)(position.Y + radius * System.Math.Sin(angle)), 
                            0f).SetZ();
                }

                var eventArgs = new ActionArgs { Position = position, Process = true, Type = OrbwalkerType.Movement };
                CallOnAction(eventArgs);

                if (eventArgs.Process && Player.IssueOrder(GameObjectOrder.MoveTo, position))
                {
                    lastMovementOrderTick = Variables.TickCount;
                }
            }
        }

        #endregion
    }
}