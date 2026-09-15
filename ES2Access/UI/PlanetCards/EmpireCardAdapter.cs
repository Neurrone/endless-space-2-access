namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// The empire page's planet card, <c>PlanetCard</c> - which widget of that prefab is which, and
    /// nothing else. What is DONE with them is <see cref="PlanetCardReader"/>'s; the page fills in the
    /// drops and the id namespace.
    ///
    /// ONE adapter for BOTH of the panel's display modes. The panel switches a card between showing
    /// what can be done to the world and showing who lives on it by hiding widgets
    /// (<c>PlanetCard.Bind</c> :243-262 turns off the deposits, the anomalies, the curiosities and the
    /// actions table in Population mode, and binds the population ring only there), so the mode is
    /// never asked here or in the reader: a widget the game is not drawing is the same fact as a
    /// widget the prefab never had, and the reader's own drawing tests answer both.
    ///
    /// A member the prefab has no widget for is simply not overridden, which is the base class's null.
    /// </summary>
    public sealed class EmpireCardAdapter : PlanetCardAdapter
    {
        private readonly PlanetCard _card;

        public EmpireCardAdapter(PlanetCard card)
        {
            _card = card;
        }

        /// <summary>The card the adapter is reading, for the page's own carry and drop handlers - the
        /// panel's transfer takes the CARD, not the planet.</summary>
        public PlanetCard Card
        {
            get { return _card; }
        }

        public override Planet Planet
        {
            get { return _card.Planet; }
        }

        /// <summary>The colony WHOEVER owns it - the card binds the colony of the system the table row
        /// stands for, falling back to the world's main one (<c>PlanetCard.Bind</c> :218), so a rival's
        /// colony is what a rival's card draws its ring from.</summary>
        public override ColonizedPlanet Colony
        {
            get { return _card.ColonizedPlanet; }
        }

        public override ColonizedPlanet PlayerColony
        {
            get { return PopulationRings.Settled(_card.ColonizedPlanet); }
        }

        public override AgeTransform Root
        {
            get { return _card.AgeTransform; }
        }

        public override AgePrimitiveLabel NameLabel
        {
            get { return _card.PlanetNameLabel; }
        }

        // StatusLabel is not overridden: this prefab draws no status label at all. It says what has
        // become of the world by TINTING the name instead (<c>RefreshPlanetName</c> :441-464 picks the
        // owning empire's colour, or the "colonizable"/"uncolonizable" one), and a colour is not text
        // to read - so the reader falls back to the model's own answer for both the spoken state and
        // the sentence behind it.

        public override AgeTransform TypeGroup
        {
            get { return _card.PlanetTypeGroup; }
        }

        /// <summary>The type's own label, which carries the reference page for that kind of world:
        /// <c>RefreshPlanetTypes</c> :477 writes the element's own Description onto this label's
        /// tooltip, exactly as the star system page's card does (measured 2026-09-15 on the live
        /// cards - every card answered a tooltip whose content is
        /// <c>%PlanetType&lt;kind&gt;Description</c>).</summary>
        public override AgePrimitiveLabel TypeLabel
        {
            get { return _card.PlanetTypeLabel; }
        }

        // SizeLabel is not overridden: this prefab draws the size as a PICTURE, scaling the planet
        // image by it in five steps and writing no words anywhere (<c>RefreshPlanetImage</c>
        // :415-438). The reader reads the size off the model, as it does on every card.

        public override AgeTransform GameplayTypeTable
        {
            get { return _card.PlanetGameplayTypesTable; }
        }

        public override AgeTransform AnomaliesTable
        {
            get { return _card.AnomalyItemsTable; }
        }

        public override AgeTransform CuriositiesTable
        {
            get { return _card.CuriosityItemsTable; }
        }

        public override AgeTransform DepositsGroup
        {
            get { return _card.ResourceDepositItemsTable; }
        }

        /// <summary>This prefab's deposit item wires NEITHER of <c>ResourceDepositItem</c>'s two
        /// optional labels (measured 2026-09-14), so it draws a bare icon and the line is the
        /// resource's name alone - a figure read out here would be one nobody else can see
        /// (<c>ES2Access/UI/PlanetCardLines.cs</c> has the measurement).</summary>
        public override bool DepositsDrawAmount
        {
            get { return false; }
        }

        public override AgeTransform ImprovementWidget
        {
            get
            {
                AgePrimitiveImage image = _card.PlanetImprovementImage;
                return image == null ? null : image.AgeTransform;
            }
        }

        public override AgeTooltip ImprovementTooltip
        {
            get { return _card.PlanetImprovementTooltip; }
        }

        // ImprovementDrawsWords is not overridden - false. The card draws the specialization as a
        // small picture and nothing else (<c>RefreshPlanetImprovement</c> :531-547), so its name is
        // the title of the wrapper on its own tooltip, and a world with no specialization says
        // nothing rather than "None".

        /// <summary>The planet's own dossier, which the game hangs on the FRAME around the picture
        /// rather than on the picture (<c>RefreshPlanetImage</c> :417 points it at the planet
        /// wrapper), so the reader aims it at the tooltip's own widget.</summary>
        public override AgeTooltip PlanetTooltip
        {
            get { return _card.PlanetImageTooltip; }
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

        /// <summary>The card swaps its two output strips at bind on exactly this question
        /// (<c>Bind</c> :229-242) - has the world been settled, or has the player already ordered a
        /// colonization - so it is the question here. It is NOT the card's own
        /// <c>ColonizedPlanet</c>: a world with a colonization pending is drawn with the numbers of
        /// the colony it is about to be, and that field is still null for it.</summary>
        public override bool FidsiDrawsNumbers
        {
            get
            {
                Planet planet = _card.Planet;
                return planet != null
                    && (planet.ColonizedPlanet != null || _card.PlayerGhostColonizedPlanet != null);
            }
        }

        /// <summary>Whose figures those are: the colony's where the card has one, and the PLANET's
        /// while a colonization is only pending - which is the object the card itself reads them
        /// from.</summary>
        public override Amplitude.Unity.Simulation.SimulationObject FidsiSource
        {
            get
            {
                ColonizedPlanet colony = _card.ColonizedPlanet;
                if (colony != null)
                {
                    return colony.SimulationObject;
                }

                Planet planet = _card.Planet;
                return planet == null ? null : planet.SimulationObject;
            }
        }

        // DeclareFidsiDossiers is not overridden - false, which is what this card offers today. Whether
        // the five figures should get a dossier each here is an open question for the owner.

        /// <summary>The card's single population ring, which the panel binds and shows only in its
        /// Population mode (<c>Bind</c> :258-259) and hides again in the other - so the ring's own
        /// drawn-ness is what decides whether the card has slots, with no mode test anywhere.</summary>
        public override PlanetPopulationEnumerator RingMarkers
        {
            get { return _card.PlanetCardPopulationEnumerator; }
        }

        public override PlanetPopulationEnumerator RingTarget
        {
            get { return _card.PlanetCardPopulationEnumerator; }
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
    }
}
