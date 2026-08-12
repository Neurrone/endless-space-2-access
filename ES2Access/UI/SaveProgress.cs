using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The one thing this game reports with no words at all: while a save is being written it shows a
    /// spinning icon and nothing else (<c>GuiManager.UpdateGameWindowsVisibility</c> :1578 shows
    /// <c>AnimatedLoadingIconWindow</c> exactly while <c>IsSaving</c>), so a player who cannot see it
    /// has no way to tell that a save - a manual one, a quick save, or the autosave at the end of a
    /// turn - is still in flight, and the game refuses to quit until it is done
    /// (<c>GameMenuModalWindow</c> :189-203).
    ///
    /// The FLAG is the authority here, not the window. Every save path sets it
    /// (<c>GameManager.Save</c> :780) and every completion and failure path clears it
    /// (<c>LoadSaveModalWindow.OnSaveCompletion</c>, <c>GameServerState_Autosave</c>,
    /// <c>GuiManager.OnQuickSaveCompletion</c>), while the window is that flag AND <c>GameReady</c>
    /// (<c>SetGameWindowVisibility</c> :1592) - so watching the window would stay silent for a save
    /// written while the game is still coming up, which is exactly a moment the player must not quit.
    ///
    /// Its setter raises <c>SavingStatusChanged</c> on CHANGE only (<c>GuiManager</c> :378-396), which
    /// is why this subscribes rather than polling the flag once a frame: a save that begins and ends
    /// inside one frame still reports both halves, and every event received is a genuine flip needing
    /// no de-duplication of its own.
    ///
    /// The handler runs inside the game's own property setter, so it only RECORDS; <see cref="Tick"/>,
    /// called from the per-frame pump, is what speaks. Queued, never interrupting: a save being
    /// written is never more urgent than what the player just asked for.
    ///
    /// A save that FAILS clears the same flag, after the game raises its own error dialog - which the
    /// mod announces as the screen it is - so the finish line means "the game has stopped writing", and
    /// the dialog is what tells the two apart. Distinguishing them here would mean hooking every one of
    /// the game's exception handlers for a case its own words already cover.
    ///
    /// The subscription is handed back in <see cref="Stop"/>, so a reload leaves nothing of this on a
    /// service that outlives the mod. A reload that lands in the MIDDLE of a save reports only the
    /// finish, which is the half that matters - "the game is safe to leave now" is true whether or not
    /// this assembly was there to hear the save start.
    /// </summary>
    internal sealed class SaveProgress
    {
        /// <summary>The flips the game has handed us since the last frame, newest last - each entry is
        /// what <c>IsSaving</c> became. Cleared every tick, so a flip that arrives while the mod is
        /// being torn down is simply never spoken.</summary>
        private readonly List<bool> _pending = new List<bool>();

        /// <summary>The service we are subscribed to. It outlives a game but not the process, and comes
        /// back as a different instance if the manager is ever released - so it is compared, not
        /// assumed.</summary>
        private IGuiGameWindowService _service;

        /// <summary>Say whatever the game reported since the last frame.</summary>
        public void Tick()
        {
            IGuiGameWindowService service = Service();
            if (service != _service)
            {
                Attach(service);
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                Voice.Say(
                    ModStrings.Get(_pending[i] ? ModStrings.SaveStarted : ModStrings.SaveFinished),
                    false
                );
            }

            _pending.Clear();
        }

        /// <summary>Give the subscription back - a handler left on the game's event would call into an
        /// assembly nobody can reach any more.</summary>
        public void Stop()
        {
            Attach(null);
        }

        private void Attach(IGuiGameWindowService service)
        {
            if (_service != null)
            {
                try
                {
                    _service.SavingStatusChanged -= SavingStatusChanged;
                }
                catch (Exception e)
                {
                    Log.Warn("save: unsubscribing threw: " + e);
                }
            }

            _service = service;
            _pending.Clear();
            if (_service == null)
            {
                return;
            }

            try
            {
                _service.SavingStatusChanged += SavingStatusChanged;
            }
            catch (Exception e)
            {
                Log.Warn("save: subscribing threw: " + e);
                _service = null;
            }
        }

        /// <summary>The watcher itself. It runs inside the game's own setter, alongside the window
        /// visibility pass it triggers, so it records and returns: speaking here would put speech
        /// outside the pump, and throwing here would break the game's own save bookkeeping.</summary>
        private void SavingStatusChanged(object sender, EventArgs e)
        {
            try
            {
                IGuiGameWindowService service = _service;
                if (service != null)
                {
                    _pending.Add(service.IsSaving);
                }
            }
            catch (Exception exception)
            {
                Log.Warn("save: recording a saving flip threw: " + exception);
            }
        }

        private static IGuiGameWindowService Service()
        {
            try
            {
                return Services.GetService<IGuiGameWindowService>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
