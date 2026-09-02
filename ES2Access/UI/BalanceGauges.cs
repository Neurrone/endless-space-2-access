using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// A bar split between two things, read as the split with each half under the game's own name for
    /// its side - "Projectile 65%, Energy 35%".
    ///
    /// The gauge writes no text anywhere. It says what it says by how far each half is drawn out from
    /// the centre (<c>RepartitionHorizontalGauge.Refresh</c> turns a value into a percentage of the
    /// bar's half-width): both halves are anchored at the middle and stretched to their own side by
    /// HALF of their share, and they reach in opposite directions, so each has to be measured from the
    /// middle its own way. An earlier reading of the right half against the bar's far END said 163%
    /// for a half drawn at 37%.
    ///
    /// A half worth nothing ends up at zero width and the game hides it, and a hidden half is skipped
    /// rather than read as "0%" - which also drops the whole line for a bar the game gave nothing to
    /// at all (<c>Refresh</c> hides both halves when the total is zero).
    ///
    /// The two side names are the game's own strings rather than whatever a host prefab happens to
    /// draw beside the bar, because the hosts do not agree: the ship designer heads its two columns
    /// with labels holding exactly these two keys, and the Academy's named-ship panel heads the same
    /// two columns with bare ICONS and no label at all. Both bars take the same pair of words - a
    /// defensive bar's halves are the plating and shield absorptions that projectile and energy
    /// weapons are respectively stopped by, which is what the columns' own tooltips say.
    /// </summary>
    public static class BalanceGauges
    {
        private const string ProjectileTitle = "%ShipDesignProjectileTitle";

        private const string EnergyTitle = "%ShipDesignEnergyTitle";

        /// <summary>The split the bar is drawn at, or nothing where neither half is drawn.</summary>
        public static string Text(RepartitionHorizontalGauge gauge)
        {
            if (gauge == null)
            {
                return null;
            }

            try
            {
                MessageBuilder message = new MessageBuilder();
                AgeTransform left = gauge.LeftGauge;
                AgeTransform right = gauge.RightGauge;
                // Content: which half of the gauge contributes its figure to the phrase.
                if (AgeWidgets.Paints(left))
                {
                    message.ListItem(AgeText.Title(ProjectileTitle));
                    message.Fragment(Percent(50f - left.PercentLeft));
                }

                // Content: the same for the other half.
                if (AgeWidgets.Paints(right))
                {
                    message.ListItem(AgeText.Title(EnergyTitle));
                    message.Fragment(Percent(right.PercentRight - 50f));
                }

                return message.Build();
            }
            catch (Exception e)
            {
                Log.Warn("balance gauge: reading a bar threw: " + e);
                return null;
            }
        }

        /// <summary>The bar as a line of its own, for a panel read as the lines it draws. What the two
        /// halves ARE beyond their names is the sentence explaining the bar - "the balance between
        /// projectile and energy weapons" - so that tooltip rides along. A bar with neither
        /// half drawn is no line at all, the same as it is no substitution in a tooltip: a stop that
        /// announces nothing is a stop the player cannot tell they have landed on.
        ///
        /// The hosts disagree about WHERE that sentence hangs - on the bar on some prefabs, on the box
        /// drawn round it on others - so the bar's own is preferred and the box it sits in is the
        /// fallback, pointed at wherever it turns out to be.</summary>
        public static void Add(List<Cell> cells, RepartitionHorizontalGauge gauge, string key)
        {
            AgeTransform widget = gauge == null ? null : gauge.AgeTransform;
            if (widget == null || string.IsNullOrEmpty(Text(gauge)))
            {
                return;
            }

            RepartitionHorizontalGauge it = gauge;
            Scratch.Clear();
            AgeWidgets.EffectiveTooltips(
                widget,
                Scratch,
                TooltipReach.Own | TooltipReach.Parents,
                1
            );
            AgeTooltip tooltip = Scratch.Count == 0 ? null : Scratch[Scratch.Count - 1];
            NodeVtable vtable = GraphNodes.Readout(() => null, () => Text(it), null, tooltip);
            Cells.Add(cells, widget, ControlId.For(widget, key), vtable);
        }

        // Reused rather than allocated per call: these bars are declared on every build. Safe as one
        // buffer because the tooltip is taken out of it before the next call.
        private static readonly List<AgeTooltip> Scratch = new List<AgeTooltip>(2);

        /// <summary>How far one half was pushed out, as the share of the whole bar it stands for -
        /// through the shared percent template, because "%" is a sign rather than a word and screen
        /// readers differ on whether they voice one at all.</summary>
        private static string Percent(float reach)
        {
            return ModStrings.Format(
                ModStrings.Percent,
                (int)Math.Abs(Math.Round(reach * 2f))
            );
        }
    }
}
