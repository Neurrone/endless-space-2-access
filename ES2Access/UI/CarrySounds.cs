using System;
using ES2Access.Core.UI;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The sounds the GAME plays around a drag, played around the keyboard's carry.
    ///
    /// A mouse dragging a ship chip in the advanced battle setup hears two cues: one as the chip
    /// leaves its card (<c>EncounterPlayShipItemInteractive.OnDragStartedCb</c> :85) and one as the
    /// drag ends, whether the flotilla took the ship or refused it (<c>OnDragCompletedCb</c> :97,
    /// :101 - the game plays the same event down both branches). The keyboard's carry is that drag,
    /// so it makes the same two noises; nothing here invents a cue the mouse does not get.
    ///
    /// Keyed by the CARGO's kind, so a carry that the game gives no sound to stays silent. Only the
    /// battle setup's ships have one today: the population ring, the two queues, the designer's
    /// slots and the fleet lists are all dragged in silence by the mouse as well (measured
    /// 2026-08-29), and a mod-invented cue on them would be the keyboard sounding different from
    /// the pointer. The LOCK is silent for the same reason - the game says nothing when a chip is
    /// pinned.
    ///
    /// Hung on the carry's own lifecycle (<see cref="Core.UI.CarryState.Started"/>,
    /// <see cref="Core.UI.CarryState.Ended"/>) by <c>ModEntry.InstallAnnouncerWording</c>, which is
    /// where every other game-side word and sound is handed to the engine. The navigator that
    /// dispatches the carry keys knows nothing about any of this: which noise a drag makes is the
    /// GAME's answer, and this file is the only place that holds it.
    ///
    /// The player is the only one who can hear these, so <see cref="Posted"/> and
    /// <see cref="Last"/> are what a test has instead of ears (<c>DevProbe.Sounds</c>): they say
    /// which event the mod asked the game for and how many times.
    /// </summary>
    public static class CarrySounds
    {
        /// <summary>The game's own two Wwise events for a ship chip's drag.</summary>
        private const uint DragStarted = 951096559u;

        private const uint DragEnded = 4116586482u;

        /// <summary>How many events this load of the mod has posted, and the last one it posted -
        /// the carry's audible half, for a probe that cannot listen.</summary>
        public static int Posted;

        public static uint Last;

        /// <summary>Something has just been picked up.</summary>
        public static void Started(CarryItem item)
        {
            Play(item, DragStarted);
        }

        /// <summary>A carry has just ended: dropped, refused, or given up. All three are one drag
        /// ending, which is the game's own reading of them.</summary>
        public static void Ended(CarryItem item)
        {
            Play(item, DragEnded);
        }

        private static void Play(CarryItem item, uint sound)
        {
            if (item == null || item.Kind != BattleShipMoves.Kind)
            {
                return;
            }

            try
            {
                Gui.PlaySound(sound);
                Posted++;
                Last = sound;
            }
            catch (Exception e)
            {
                Log.Warn("carry: playing the game's drag sound threw: " + e);
            }
        }
    }
}
