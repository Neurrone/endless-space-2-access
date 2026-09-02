using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ES2Access.Core.UI.Graph;
using ES2Access.UI;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace ES2Access.Dev
{
    /// <summary>The probes about tooltips: the engine's hover pipeline, the hover delay, the icons
    /// nothing names, and the tooltip on screen right now.</summary>
    public static partial class DevProbe
    {
        /// <summary>Passed to <see cref="TooltipDelay"/> to put the game's own value back.</summary>
        public const double RestoreTooltipDelay = -1;

        /// <summary>
        /// One log line per frame naming the whole of the engine's hover-to-tooltip pipeline: what the
        /// mod is pointing the engine at, what the tooltip controller remembers pointing at, its
        /// countdown to showing, the tooltip's own content/class/target, and whether the tooltip window
        /// is up. Always false, so <c>POST /wait</c> on it records the whole passage.
        ///
        /// The countdown is the interesting number. <c>GuiTooltipController.Update</c> parks it at
        /// <c>999</c> when the delay elapses over a tooltip with neither content nor target, and only a
        /// CHANGE of hovered transform - or the tooltip's target being written again - ever resets it.
        /// </summary>
        public static bool TooltipTrace(string tag)
        {
            try
            {
                Core.Util.Log.Info("ttrace " + tag + " f=" + Time.frameCount + " " + TooltipPipeline());
            }
            catch (Exception e)
            {
                Core.Util.Log.Warn("ttrace threw: " + e.Message);
            }

            return false;
        }

        /// <summary>The same reading as <see cref="TooltipTrace"/>, answered once to a poll.</summary>
        public static string TooltipPipe()
        {
            try
            {
                return TooltipPipeline();
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string TooltipPipeline()
        {
            System.Text.StringBuilder line = new System.Text.StringBuilder();
            AgeManager age = AgeManager.Instance;
            AgeTransform over = age == null ? null : age.OverrolledTransform;
            line.Append("over=").Append(Named(over));

            Amplitude.Unity.Gui.GuiTooltipController controller = TooltipController();
            if (controller == null)
            {
                line.Append(" controller=none");
            }
            else
            {
                AgeTransform remembered = controller.OverrolledAgeTransform;
                line.Append(" ctrl=").Append(Named(remembered));
                FieldInfo timer = AccessTools.Field(
                    typeof(Amplitude.Unity.Gui.GuiTooltipController),
                    "timeBeforeShowingTooltip"
                );
                line.Append(" timer=")
                    .Append(
                        timer == null
                            ? "?"
                            : Round((float)timer.GetValue(controller)).ToString(
                                CultureInfo.InvariantCulture
                            )
                    );
                line.Append(" cur=")
                    .Append(
                        controller.CurrentTooltipWindow == null
                            ? "-"
                            : controller.CurrentTooltipWindow.name
                    );
                AgeTooltip tip = controller.OverrolledAgeTooltip;
                line.Append(" tip=").Append(Tipped(tip));
            }

            AgeTooltip aimed = over == null ? null : over.AgeTooltip;
            line.Append(" aimed=").Append(Tipped(aimed));
            line.Append(" want=").Append(Tipped(ES2Access.UI.PointerFocus.Wanted));

            GuiTooltipWindow window = Gui.GuiServiceAvailable
                ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                : null;
            line.Append(" win=")
                .Append(window == null ? "none" : (window.Shown ? "shown" : "hidden"))
                .Append(
                    window == null || window.AgeTooltip == null
                        ? ""
                        : "/" + window.AgeTooltip.Class
                );

            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            line.Append(" delay=")
                .Append(
                    gui == null
                        ? "?"
                        : Round(gui.TooltipDisplayDelay).ToString(CultureInfo.InvariantCulture)
                );
            line.Append(" notif=")
                .Append(
                    Gui.GuiNotificationService == null
                        ? "?"
                        : (Gui.GuiNotificationService.CanShowNotifications ? "can" : "no")
                );

            GraphNavigator navigator = ModEntry.Navigator;
            ControlId focused = navigator == null ? null : navigator.FocusedKey;
            line.Append(" node=")
                .Append(focused == null ? "-" : Convert.ToString(focused.StructuralKey));
            return line.ToString();
        }

        private static string Named(AgeTransform widget)
        {
            if (ReferenceEquals(widget, null))
            {
                return "null";
            }

            if (widget == null)
            {
                return "destroyed";
            }

            return widget.name;
        }

        private static string Tipped(AgeTooltip tooltip)
        {
            if (ReferenceEquals(tooltip, null))
            {
                return "-";
            }

            if (tooltip == null)
            {
                return "destroyed";
            }

            return "["
                + (string.IsNullOrEmpty(tooltip.Class) ? "noclass" : tooltip.Class)
                + " content="
                + (string.IsNullOrEmpty(tooltip.Content) ? "0" : tooltip.Content.Length.ToString())
                + (tooltip.Target == null ? " notarget" : " target")
                + (tooltip.DirtyTarget ? " dirty" : "")
                + "]";
        }

        private static Amplitude.Unity.Gui.GuiTooltipController TooltipController()
        {
            GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
            if (gui == null)
            {
                return null;
            }

            PropertyInfo property = AccessTools.Property(
                typeof(Amplitude.Unity.Gui.GuiManager),
                "GuiTooltipController"
            );
            return property == null
                ? null
                : property.GetValue(gui, null) as Amplitude.Unity.Gui.GuiTooltipController;
        }

        public static string TooltipDelay(double seconds)
        {
            return Guarded(json =>
            {
                GuiManager gui = Gui.GuiServiceAvailable ? Gui.GuiService as GuiManager : null;
                if (gui == null)
                {
                    throw new InvalidOperationException("the gui service is not up yet");
                }

                FieldInfo field = TooltipDelayField();
                float was = (float)field.GetValue(gui);
                float wanted;
                if (seconds == RestoreTooltipDelay)
                {
                    wanted = _tooltipDelayWas ?? RegisteredTooltipDelay();
                    _tooltipDelayWas = null;
                }
                else
                {
                    wanted = (float)seconds;
                    if (_tooltipDelayWas == null)
                    {
                        _tooltipDelayWas = was;
                    }
                }

                field.SetValue(gui, wanted);
                json.WritePropertyName("was");
                json.WriteValue(Round(was));
                json.WritePropertyName("now");
                json.WriteValue(Round(wanted));
                json.WritePropertyName("registry");
                json.WriteValue(Round(RegisteredTooltipDelay()));
            });
        }

        private static float? _tooltipDelayWas;

        private static FieldInfo TooltipDelayField()
        {
            FieldInfo field = AccessTools.Field(typeof(GuiManager), "tooltipDisplayDelay");
            if (field == null)
            {
                throw new MissingFieldException("GuiManager", "tooltipDisplayDelay");
            }

            return field;
        }

        // The player's own setting, untouched: it is only ever read here.
        private static float RegisteredTooltipDelay()
        {
            return Amplitude.Unity.Framework.Application.Registry.GetValue(
                GuiManager.Registers.TooltipDisplayDelay,
                0.3f
            );
        }

        /// <summary>
        /// The icons this load met that <see cref="IconNames"/> could not name.
        ///
        /// <c>tokens</c> is the tripwire and must stay EMPTY: the engine's registered token set is
        /// closed and the icon table covers all of it, so an entry here is a patch, a DLC or a mod
        /// that added one - and a word a player heard nothing for. <c>pictures</c> is an audit
        /// sample rather than a defect list: every bitmap in the game can be drawn into a panel and
        /// most of them are decoration, so this is where to look when a tooltip is missing a word
        /// and the picture that carried it needs identifying.
        /// </summary>
        public static string UnknownIcons()
        {
            return Guarded(json =>
            {
                WriteStrings(json, "tokens", IconNames.UnknownTokens);
                WriteStrings(json, "pictures", IconNames.UnknownPictures);
            });
        }

        private static void WriteStrings(JsonTextWriter json, string name, IList<string> values)
        {
            json.WritePropertyName(name);
            json.WriteStartArray();
            for (int i = 0; i < values.Count; i++)
            {
                json.WriteValue(values[i]);
            }

            json.WriteEndArray();
        }

        /// <summary>How deep under the tooltip's panel <see cref="Tooltip"/> looks.</summary>
        private const int MaxTooltipDepth = 8;

        /// <summary>
        /// The tooltip the game is drawing right now, as MEASURED - every label and every icon under
        /// its panel, each with the rectangle it occupies, in the order the panel laid them out, plus
        /// the panel FEATURES those widgets belong to and what each one was read as.
        ///
        /// It is the paired proof a tooltip claim needs. <c>/gui/graph</c> says what the mod would
        /// SPEAK for a control; a screenshot says what the player SEES; neither says why the two
        /// differ. This does: two labels the mod read in the wrong order differ here by a rectangle,
        /// and an icon the mod turned into a bare number is an entry here with an asset name and no
        /// text beside it. Both questions came up on the same tooltip and neither is answerable from
        /// the spoken form alone.
        ///
        /// The <c>features</c> array is the coverage answer. A tooltip is assembled from an ordered
        /// list of feature prefabs and the mod reads each one on its own; a feature nobody has
        /// written a reader for still reads, through the fallback, so the only place a gap can show
        /// is here - the feature's class name beside <c>"default"</c>, with the lines it produced to
        /// judge them by. Coverage belongs in a probe, never in the player's ears.
        ///
        /// An icon reports the name of the TEXTURE it is drawing, which is what the artist called the
        /// picture ("Food", "FIDS_Industry") and therefore what the column means - the only evidence
        /// available for a value the game draws with no word next to it anywhere.
        ///
        /// Requires the tooltip to be up: focus the control first, and
        /// <see cref="TooltipDelay"/><c>(0)</c> so it is up on the next frame.
        /// </summary>
        public static string Tooltip()
        {
            return Guarded(json =>
            {
                GuiTooltipWindow window = Gui.GuiServiceAvailable
                    ? Gui.GuiService.GetWindow<GuiTooltipWindow>(false)
                    : null;
                json.WritePropertyName("shown");
                json.WriteValue(window != null && window.Shown);
                if (window == null || !window.Shown || window.PanelFeaturesTable == null)
                {
                    return;
                }

                json.WritePropertyName("class");
                json.WriteValue(
                    window.AgeTooltip == null ? null : window.AgeTooltip.Class
                );

                json.WritePropertyName("features");
                json.WriteStartArray();
                IList<TooltipFeatures.Reading> features = DrawnTooltip.Features(window.AgeTooltip);
                for (int i = 0; i < features.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("feature");
                    json.WriteValue(features[i].Feature);
                    json.WritePropertyName("reader");
                    json.WriteValue(features[i].Reader);
                    json.WritePropertyName("lines");
                    json.WriteStartArray();
                    for (int line = 0; line < features[i].Lines.Count; line++)
                    {
                        json.WriteValue(features[i].Lines[line]);
                    }

                    json.WriteEndArray();
                    json.WriteEndObject();
                }

                json.WriteEndArray();

                // Every feature class the fallback reader has answered for since this assembly loaded,
                // so a feature nobody has judged surfaces in tooling rather than in a player's ears
                // (<see cref="TooltipFeatures.DefaultRead"/>). Cumulative, not this tooltip's.
                json.WritePropertyName("defaultRead");
                json.WriteStartArray();
                List<string> defaulted = new List<string>(TooltipFeatures.DefaultRead);
                defaulted.Sort(StringComparer.Ordinal);
                for (int i = 0; i < defaulted.Count; i++)
                {
                    json.WriteValue(defaulted[i]);
                }

                json.WriteEndArray();

                List<AgeTransform> found = new List<AgeTransform>();
                Gather(window.PanelFeaturesTable, found, 0);

                // Banded across the WHOLE window, which the reader deliberately no longer does: the
                // rows here are the measurement, and a feature whose reading disagrees with them is
                // the interesting case rather than a contradiction.
                json.WritePropertyName("rows");
                json.WriteStartArray();
                foreach (List<AgeTransform> row in AgeLayout.Rows(found, Itself))
                {
                    json.WriteStartArray();
                    foreach (AgeTransform widget in row)
                    {
                        WritePart(json, widget);
                    }

                    json.WriteEndArray();
                }

                json.WriteEndArray();
            });
        }

    }
}
