namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// The map's orbital planet card, <c>PlanetLabel_SystemOrbital</c> - which widget of that prefab
    /// is which, and nothing else. What is DONE with them is <see cref="PlanetCardReader"/>'s; the
    /// page fills in the id namespace, the carrier for a deposit the card draws no item for, the map's
    /// own signal lines and the quest pins.
    ///
    /// The card the game draws in orbit once the camera has come all the way in on a system. It has no
    /// population ring, no Sanctuary band, no outpost band and no rename button, and in place of the
    /// colony buttons the other two prefabs draw it carries the row of FLEET actions a fleet in the
    /// system could perform on the world - which is why so many members below appear on no other card.
    ///
    /// A member the prefab has no widget for is simply not overridden, which is the base class's null.
    /// </summary>
    public sealed class OrbitalCardAdapter : PlanetCardAdapter
    {
        private readonly PlanetLabel_SystemOrbital _card;

        public OrbitalCardAdapter(PlanetLabel_SystemOrbital card)
        {
            _card = card;
        }

        /// <summary>The card the adapter is reading, for anything the PAGE resolves off the label
        /// itself.</summary>
        public PlanetLabel_SystemOrbital Card
        {
            get { return _card; }
        }

        public override Planet Planet
        {
            get { return _card.Planet; }
        }

        // Colony is NOT overridden, and deliberately: this prefab draws no population ring and no
        // population figure anywhere, so a card that answered with one would put the colony's people
        // into a buffer whose whole rule is that it is the CARD's face. The five outputs still come
        // off the colony - FidsiSource below reads it from the card directly, which is where the
        // card's own refresh reads them from.

        /// <summary>The Sanctuary sitting on this world, which is what the card's haunt icon warns
        /// about - whose it is decides which of the game's two titles the icon is called by.</summary>
        public override ColonizedPlanet GhostColony
        {
            get { return _card.GhostColonizedPlanet; }
        }

        /// <summary>The card's own container rather than the label around it - the widget the planet's
        /// dossier hangs on (measured 2026-09-15: <c>PlanetInfoTooltip.AgeTransform</c> is
        /// <c>PlanetOrbitalCard</c>, the container itself), and so the rectangle a mouse rests in to
        /// raise it.</summary>
        public override AgeTransform Root
        {
            get { return _card.PlanetOrbitalCardContainer ?? _card.AgeTransform; }
        }

        public override AgePrimitiveLabel NameLabel
        {
            get { return _card.PlanetName; }
        }

        /// <summary>This prefab writes how big the world is and what kind it is as ONE line
        /// (<c>RefreshPlanetInformation</c> :455, through the game's own
        /// <c>%PlaneSizeAndTypeFormat</c>), so the drawn words are what the row says and there are no
        /// separate type or size labels to read or to hang a reference page on.</summary>
        public override AgePrimitiveLabel SizeAndTypeLabel
        {
            get { return _card.PlanetSizeAndType; }
        }

        public override AgePrimitiveLabel StatusLabel
        {
            get { return _card.ColonizeStatus; }
        }

        /// <summary>Measured 2026-09-15 on every drawn card: this prefab wires NO tooltip on the
        /// status label, and no control on it either (<c>PlanetLabel.RefreshPlanetStatus</c> :234 only
        /// writes its sentence where the prefab has one, and :457 passes no status button), so the
        /// state line here is the drawn words and nothing more. Asked of the widget rather than
        /// hard-coded null, so a prefab that grows one is read without a change here.</summary>
        public override AgeTooltip StatusTooltip
        {
            get
            {
                AgePrimitiveLabel status = _card.ColonizeStatus;
                return status == null ? null : AgeWidgets.Raw(status.AgeTransform);
            }
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

        /// <summary>This prefab's deposit item wires NEITHER of <c>ResourceDepositItem</c>'s two
        /// optional labels (measured 2026-09-15 on every drawn card: both fields null), so it draws a
        /// bare icon and the line is the resource's name alone - the empire card's prefab over
        /// again.</summary>
        public override bool DepositsDrawAmount
        {
            get { return false; }
        }

        public override AgeTooltip PlanetTooltip
        {
            get { return _card.PlanetInfoTooltip; }
        }

        /// <summary>The map's row IS the card: a mouse resting on the planet in orbit raises this
        /// dossier, so it is the row's own tooltip section rather than a child of the Tooltips region
        /// (owner ruling 2026-09-15 - the one surface where it is).</summary>
        public override bool PlanetTooltipIsCardSection
        {
            get { return true; }
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

        /// <summary>The card writes numbers for a COLONY and only while it is drawing that strip -
        /// the two questions its own reading asks (<c>RefreshFIDSI</c> :1016-1024 swaps the strips,
        /// and a world with no colony has no numbers to write).</summary>
        public override bool FidsiDrawsNumbers
        {
            get
            {
                FidsiEnumerator fidsi = _card.FidsiEnumerator;
                // Flow control: which of the card's two output strips is read - lines of the card's
                // buffer, which no gate ever sees.
                return _card.ColonizedPlanet != null
                    && fidsi != null
                    && AgeWidgets.Visible(fidsi.AgeTransform);
            }
        }

        /// <summary>And the pips only where the card is drawing the pip table for a world this empire
        /// has surveyed: an unrevealed node hides that table wholesale
        /// (<c>RefreshAsUnrevealedNode</c>), and a rating read off a world the map is drawing as an
        /// unknown would say what the picture refuses to.</summary>
        public override bool FidsiDrawsRatings
        {
            get
            {
                // Flow control, as above: whether the pip lines are among the card's buffer lines.
                return _card.IsNodeRevealed && AgeWidgets.Visible(_card.FidsiScoreTable);
            }
        }

        public override Amplitude.Unity.Simulation.SimulationObject FidsiSource
        {
            get
            {
                ColonizedPlanet colony = _card.ColonizedPlanet;
                return colony == null ? null : colony.SimulationObject;
            }
        }

        /// <summary>How long an outpost has left before it becomes a colony, which this prefab draws
        /// as a bare countdown instead of the other card's growth line - the sentence behind it is
        /// filled for a FOREIGN outpost only (<c>docs/planets.md</c>).</summary>
        public override AgePrimitiveLabel OutpostTimer
        {
            get { return _card.OutpostTimer; }
        }

        public override AgeTooltip OutpostTooltip
        {
            get { return _card.OutpostTooltip; }
        }

        public override AgeControlButton ColonizeButton
        {
            get { return _card.ColonizeButton; }
        }

        // SpecializationButton, ReduceAnomalyButton, TerraformButton and RenameButton are not
        // overridden: this prefab draws none of them. Its own TerraformationButton and
        // AnomalyReductionButton below are FLEET actions - a juggernaut sent to the world - which is a
        // different thing from the colony's own build order of nearly the same name.

        public override AgeControlButton VodyaniHintButton
        {
            get { return _card.VodyaniHintButton; }
        }

        public override AgeControlButton UmbralChoirHintButton
        {
            get { return _card.UmbralChoirHintButton; }
        }

        public override AgeControlButton BuyOutpostButton
        {
            get { return _card.BuyOutpostButton; }
        }

        public override AgeControlButton MinorFactionButton
        {
            get { return _card.MinorFactionButton; }
        }

        public override AgeTransform PirateLairGroup
        {
            get { return _card.PirateLairGroup; }
        }

        public override AgeControlButton TerraformationButton
        {
            get { return _card.TerraformationButton; }
        }

        public override AgeControlButton RestorationButton
        {
            get { return _card.RestorationButton; }
        }

        public override AgeControlButton AnomalyReductionButton
        {
            get { return _card.AnomalyReductionButton; }
        }

        public override AgeControlButton MiningProbeButton
        {
            get { return _card.MiningProbeButton; }
        }

        public override AgeControlButton DestroyButton
        {
            get { return _card.DestroyButton; }
        }

        public override AgeControlButton InProgressTerraformation
        {
            get { return _card.InProgressTerraformationButton; }
        }

        public override AgeControlButton InProgressRestoration
        {
            get { return _card.InProgressRestorationButton; }
        }

        public override AgeControlButton InProgressAnomalyReduction
        {
            get { return _card.InProgressAnomalyReductionButton; }
        }

        /// <summary>A world colonized and lost, which the card marks with a wordless icon
        /// (:353-381).</summary>
        public override AgeTransform DecayIcon
        {
            get { return _card.HuntingGroundsIcon; }
        }

        public override AgeTransform OutpostCancelIcon
        {
            get { return _card.OutpostCancelIcon; }
        }

        public override AgeTransform HauntIcon
        {
            get
            {
                AgePrimitiveImage icon = _card.HauntIcon;
                return icon == null ? null : icon.AgeTransform;
            }
        }
    }
}
