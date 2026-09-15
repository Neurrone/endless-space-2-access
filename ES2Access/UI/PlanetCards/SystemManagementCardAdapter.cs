namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// The star system page's planet card, <c>PlanetLabel_SystemManagement</c> - which widget of that
    /// prefab is which, and nothing else. What is DONE with them is
    /// <see cref="PlanetCardReader"/>'s; the page fills in the click, the drops and the id namespace.
    ///
    /// A member the prefab has no widget for is simply not overridden, which is the base class's null.
    /// </summary>
    public sealed class SystemManagementCardAdapter : PlanetCardAdapter
    {
        private readonly PlanetLabel_SystemManagement _card;

        public SystemManagementCardAdapter(PlanetLabel_SystemManagement card)
        {
            _card = card;
        }

        /// <summary>The card the adapter is reading, for the page's own drop handlers - the game's own
        /// drag client takes the LABEL, not the planet.</summary>
        public PlanetLabel_SystemManagement Card
        {
            get { return _card; }
        }

        public override Planet Planet
        {
            get { return _card.Planet; }
        }

        /// <summary>The colony WHOEVER owns it: the label binds whatever colony the planet holds
        /// (<c>PlanetLabel.BindPlanet</c> takes it straight off <c>Planet.ColonizedPlanet</c>), so an
        /// enemy outpost's card holds the enemy's colony and the card draws their ring.</summary>
        public override ColonizedPlanet Colony
        {
            get { return _card.ColonizedPlanet; }
        }

        public override ColonizedPlanet PlayerColony
        {
            get { return PopulationRings.Settled(_card.ColonizedPlanet); }
        }

        public override ColonizedPlanet GhostColony
        {
            get { return _card.GhostColonizedPlanet; }
        }

        public override ColonizedPlanet PlayerGhostColony
        {
            get { return PopulationRings.Settled(_card.GhostColonizedPlanet); }
        }

        public override AgeTransform Root
        {
            get { return _card.AgeTransform; }
        }

        public override AgePrimitiveLabel NameLabel
        {
            get { return _card.PlanetTitle; }
        }

        public override AgePrimitiveLabel StatusLabel
        {
            get { return _card.PlanetStatus; }
        }

        public override AgeTooltip StatusTooltip
        {
            get { return AgeWidgets.Raw(StatusTransform); }
        }

        /// <summary>The hint is set on the status label itself, by
        /// <c>PlanetLabel.RefreshPlanetStatus</c> :232.</summary>
        public override AgeTransform StatusHintWidget
        {
            get { return StatusTransform; }
        }

        private AgeTransform StatusTransform
        {
            get
            {
                AgePrimitiveLabel status = _card.PlanetStatus;
                return status == null ? null : status.AgeTransform;
            }
        }

        public override AgeTransform TypeGroup
        {
            get { return _card.PlanetTypeGroup; }
        }

        public override AgePrimitiveLabel TypeLabel
        {
            get { return _card.PlanetType; }
        }

        /// <summary>The prefab has a size label, and the card hides the group holding it at bind
        /// (<c>BindPlanet</c> :348) and never shows it again - so the label is never written and never
        /// drawn. It is named here because it IS a widget of this prefab; the reader's own drawing
        /// tests are what leave it out.</summary>
        public override AgePrimitiveLabel SizeLabel
        {
            get { return _card.PlanetSize; }
        }

        public override AgeTransform GameplayTypeTable
        {
            get { return _card.PlanetGameplayTypeTable; }
        }

        public override AgeTransform AnomaliesTable
        {
            get { return _card.PlanetAnomaliesTable; }
        }

        public override AgeTransform CuriositiesTable
        {
            get { return _card.PlanetCuriositiesTable; }
        }

        public override AgeTransform DepositsGroup
        {
            get { return _card.ResourceDepositsGroup; }
        }

        /// <summary>This prefab's deposit item wires <c>AmountLabel</c>, so the figure is drawn and is
        /// read (<c>ResourceDepositItem.Refresh</c> :40-43 writes it).</summary>
        public override bool DepositsDrawAmount
        {
            get { return true; }
        }

        public override PlanetDepletionStatusItem Depletion
        {
            get { return _card.PlanetDepletionStatusItem; }
        }

        public override AgeTransform ImprovementWidget
        {
            get { return _card.ImprovementStatus; }
        }

        public override AgeTooltip ImprovementTooltip
        {
            get { return _card.ImprovementTooltip; }
        }

        /// <summary>This prefab writes the improvement's name, "none" or "being built" into three
        /// labels of its own (<c>RefreshPlanetImprovement</c> :1345-1390).</summary>
        public override bool ImprovementDrawsWords
        {
            get { return true; }
        }

        public override AgeTooltip PlanetTooltip
        {
            get { return _card.PlanetTooltipFrame; }
        }

        public override FidsiEnumerator Fidsi
        {
            get { return _card.FidsiEnumerator; }
        }

        public override AgeTransform FidsiScoreTable
        {
            get { return _card.FidsiScoreTable; }
        }

        public override FidsiParametersGuiElement FidsiParameters
        {
            get { return _card.FidsiParametersGuiElement; }
        }

        /// <summary>The card draws numbers for a COLONY and rating pips for a world nobody has
        /// settled, and swaps the two strips on exactly that question
        /// (<c>BindPlanet</c> :358-368), so it is the question here.</summary>
        public override bool FidsiDrawsNumbers
        {
            get { return _card.ColonizedPlanet != null; }
        }

        public override Amplitude.Unity.Simulation.SimulationObject FidsiSource
        {
            get
            {
                ColonizedPlanet colony = _card.ColonizedPlanet;
                return colony == null ? null : colony.SimulationObject;
            }
        }

        public override bool DeclareFidsiDossiers
        {
            get { return true; }
        }

        /// <summary>Whichever of the world's two rings the card is drawing - it keeps a simple one for
        /// the ordinary view and a detailed one it swaps in under a mouse.</summary>
        public override PlanetPopulationEnumerator RingMarkers
        {
            get
            {
                return _card.PlanetPopulationEnumeratorSimple != null
                    && _card.PlanetPopulationEnumeratorSimple.Shown
                    ? (PlanetPopulationEnumerator)_card.PlanetPopulationEnumeratorSimple
                    : _card.PlanetPopulationEnumeratorFocused;
            }
        }

        /// <summary>The ring whose own <c>CanAcceptPopulationDrop</c> answers a drop, which for a
        /// planet is always the focused one whichever is drawn.</summary>
        public override PlanetPopulationEnumerator RingTarget
        {
            get { return _card.PlanetPopulationEnumeratorFocused; }
        }

        public override PlanetPopulationEnumerator GhostRingMarkers
        {
            get { return _card.GhostPopulationEnumeratorFocused; }
        }

        public override AgeTransform GhostGroup
        {
            get { return _card.GhostGroup; }
        }

        public override AgePrimitiveLabel GhostTitle
        {
            get { return _card.GhostTitle; }
        }

        public override AgePrimitiveLabel GhostPopulationCount
        {
            get { return _card.GhostPopulationCount; }
        }

        public override FidsiEnumerator GhostFidsi
        {
            get { return _card.GhostFidsiEnumerator; }
        }

        public override AgeTransform GhostTraitorButton
        {
            get { return _card.TraitorButton; }
        }

        public override AgeTransform OutpostGroup
        {
            get { return _card.OutpostGroup; }
        }

        public override AgePrimitiveLabel OutpostOwnerLabel
        {
            get { return _card.OutpostOwnerLabel; }
        }

        public override AgePrimitiveLabel OutpostBottomCaption
        {
            get { return _card.OutpostBottomCaption; }
        }

        public override GrowthGaugeItem GrowthLine
        {
            get { return _card.GrowthLine; }
        }

        public override AgeTransform OutpostActionsTable
        {
            get { return _card.OutpostActionsTable; }
        }

        public override AgeControlToggle DecolonizeToggle
        {
            get { return _card.DecolonizeToggle; }
        }

        public override AgeControlButton ColonizeButton
        {
            get { return _card.ColonizeButton; }
        }

        public override AgeControlButton SpecializationButton
        {
            get { return _card.BuildInfrastructureButton; }
        }

        public override AgeControlButton ReduceAnomalyButton
        {
            get { return _card.ReduceAnomalyButton; }
        }

        public override AgeControlButton TerraformButton
        {
            get { return _card.TerraformButton; }
        }

        public override AgeTransform RenameButton
        {
            get { return _card.PlanetRenameButton; }
        }

        /// <summary>The prefab wires one in-progress juggernaut button, which no reader has ever
        /// declared; whether the system card should offer the cancel the map's card offers is an open
        /// question for the owner, so it is named here and composed nowhere.</summary>
        public override AgeControlButton InProgressTerraformation
        {
            get { return _card.InProgressTerraformationButton; }
        }
    }
}
