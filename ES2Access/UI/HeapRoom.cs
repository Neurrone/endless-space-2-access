using System;
using System.Runtime.InteropServices;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// Gives the game's garbage collector room to work in, once per loaded game, so its pauses come
    /// less often.
    ///
    /// The game runs Unity 5.5's Mono with the Boehm collector: every collection stops the game and
    /// traces all live data, most of a second on a mid-game save however little of it is garbage. In
    /// a fresh session the heap has only 70-90 MB free above that data, so it fills - and the game
    /// pauses - every few seconds. The heap is never handed back to Windows while the process lives,
    /// so growing it once keeps the room for the session: measured on the empire summary, a pause
    /// every 6 s became one every 20 s (gc-handover.md, sections 3 and 6).
    ///
    /// Measured, not grown unconditionally: the mod reloads often and the heap belongs to the
    /// process, so a second growth would only take more of the player's memory. Free room is read
    /// straight after a collection, because between collections "used" counts garbage too. Grown in
    /// 16 MB blocks rather than one array because the collector is conservative: a stray value that
    /// looks like a pointer into one huge array would keep all of it alive for the session.
    ///
    /// When: on the first frame of a game after a load, with a forced collection that the load hides;
    /// and when the mod starts with a game already running (a hot reload), at the next collection the
    /// game makes on its own rather than a forced one. Never at the main menu. If the runtime does not
    /// export the two heap figures the check switches itself off.
    /// </summary>
    public static class HeapRoom
    {
        /// <summary>Free heap to have after a collection. Past roughly this much the collector stops
        /// waiting for the heap to fill and collects after a fixed amount of allocation instead, so
        /// more buys almost nothing (measured: about 345 MB allocated between collections with
        /// 509 MB free).</summary>
        private const long TargetFree = 450L * 1024 * 1024;

        private const int BlockSize = 16 * 1024 * 1024;

        private static bool _unavailable;

        /// <summary>The game whose room has been seen to, or is being waited on.</summary>
        private static object _game;

        /// <summary>Whether this load of the mod has seen no game, or the loading window - so the next
        /// game to appear is a load rather than one that was already running when the mod started.
        /// </summary>
        private static bool _sawNoGame;

        /// <summary>The collection count to wait past before measuring, or -1 when not waiting.
        /// </summary>
        private static int _waitPast = -1;

        [DllImport("mono")]
        private static extern long mono_gc_get_heap_size();

        [DllImport("mono")]
        private static extern long mono_gc_get_used_size();

        public static void Tick()
        {
            if (_unavailable)
            {
                return;
            }

            try
            {
                object game = LoadedGame();
                if (game == null)
                {
                    _sawNoGame = true;
                    _game = null;
                    _waitPast = -1;
                    return;
                }

                if (ReferenceEquals(game, _game))
                {
                    if (_waitPast >= 0 && GC.CollectionCount(0) != _waitPast)
                    {
                        _waitPast = -1;
                        Measure("after the game's own collection");
                    }

                    return;
                }

                _game = game;
                if (_sawNoGame)
                {
                    GC.Collect();
                    Measure("after the game loaded");
                }
                else
                {
                    _waitPast = GC.CollectionCount(0);
                }
            }
            catch (Exception e)
            {
                _unavailable = true;
                Log.Warn("heap room: switched off: " + e.GetType().Name + ": " + e.Message);
            }
        }

        public static void Reset()
        {
            _game = null;
            _sawNoGame = false;
            _waitPast = -1;
        }

        /// <summary>The game being played, or null at a menu and while the loading window is up.
        /// The loading window is asked only once the GUI's windows exist: before that the question
        /// writes an engine error per call (<c>StateProbes.Resolve</c>).</summary>
        private static object LoadedGame()
        {
            if (!Gui.GuiServiceAvailable)
            {
                return null;
            }

            GuiManager gui = Gui.GuiService as GuiManager;
            if (gui == null || !gui.GuiWindowsLoaded || gui.IsInLoadingWindow)
            {
                return null;
            }

            return Gui.Game;
        }

        private static void Measure(string when)
        {
            long heap = mono_gc_get_heap_size();
            long used = mono_gc_get_used_size();
            long free = heap - used;
            if (free >= TargetFree)
            {
                Log.Info("heap room: " + Mb(free) + " MB free " + when + "; nothing to add");
                return;
            }

            int blocks = (int)((TargetFree - free + BlockSize - 1) / BlockSize);
            byte[][] held = new byte[blocks][];
            for (int i = 0; i < blocks; i++)
            {
                held[i] = new byte[BlockSize];
            }

            // Let go of every block before returning, so a stale stack slot that still holds the
            // outer array keeps nothing but the array itself.
            for (int i = 0; i < blocks; i++)
            {
                held[i] = null;
            }

            Log.Info(
                "heap room: "
                    + Mb(free)
                    + " MB free "
                    + when
                    + "; grew the heap by "
                    + Mb((long)blocks * BlockSize)
                    + " MB to "
                    + Mb(mono_gc_get_heap_size())
                    + " MB"
            );
        }

        private static long Mb(long bytes)
        {
            return bytes / (1024 * 1024);
        }
    }
}
