using System;
using Amplitude;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;

namespace ES2Access.UI.PlanetCards
{
    /// <summary>
    /// WHAT ONE PLANET CARD IS MADE OF, per prefab.
    ///
    /// This game draws a planet card out of three different prefabs on four surfaces - the star
    /// system page's <c>PlanetLabel_SystemManagement</c>, the empire page's <c>PlanetCard</c> in both
    /// of its display modes, and the map's <c>PlanetLabel_SystemOrbital</c> - and each of them was
    /// read by a page of its own holding its own private list of "what a card has". Every gap found
    /// in those readers was one list missing an entry another list had, so the lists are replaced by
    /// this one: a subclass per prefab says which widget is which, and
    /// <see cref="PlanetCardReader"/> composes the same readout, the same buffer and the same
    /// children out of whatever it is handed.
    ///
    /// EVERY MEMBER IS NULL WHERE THE PREFAB HAS NO SUCH WIDGET. That is the whole of how the three
    /// prefabs differ here - there is no page test inside the reader and no hook a prefab can use to
    /// add behaviour of its own. A member the prefab HAS is answered with the widget whether or not
    /// the game is drawing it this frame: drawn-ness is the reader's question, asked of the game with
    /// the game's own test, because a widget the game has retired and a widget the prefab never had
    /// are different facts and only one of them is constant.
    ///
    /// Members are properties rather than fields so that a subclass can answer from whichever of two
    /// swapped widgets the game is drawing (the system card keeps a simple population ring and a
    /// detailed one; every card keeps two FIDSI strips).
    ///
    /// The last group is the PAGE's: a card's click, its drops, its id namespace and the readings a
    /// page assembles for itself. Those are fields, set by the page when it builds the adapter, and
    /// null means "this page offers none".
    /// </summary>
    public abstract class PlanetCardAdapter
    {
        // ---- the world ----

        /// <summary>The planet the card is bound to. Never null - a card without one is not read at
        /// all.</summary>
        public abstract Planet Planet { get; }

        /// <summary>The colony on this world, WHOEVER owns it - the same object the card binds its
        /// population ring to (<c>PlanetLabel.BindPlanet</c> takes it straight off
        /// <c>Planet.ColonizedPlanet</c>, so an enemy outpost's card holds the enemy's colony), and so
        /// the one to read the ring's contents from. Null on a world nobody has settled.</summary>
        public virtual ColonizedPlanet Colony
        {
            get { return null; }
        }

        /// <summary>The colony on this world when it is the PLAYER's, or null - the card of an
        /// unsettled world, or of somebody else's colony, is neither a drag source nor a drop
        /// target.</summary>
        public virtual ColonizedPlanet PlayerColony
        {
            get { return null; }
        }

        /// <summary>The Sanctuary sitting on this world, whoever owns it.</summary>
        public virtual ColonizedPlanet GhostColony
        {
            get { return null; }
        }

        /// <summary>The Sanctuary sitting on this world when it is the PLAYER's, or null - a rival's
        /// draws its title and its population figure and nothing that can be worked.</summary>
        public virtual ColonizedPlanet PlayerGhostColony
        {
            get { return null; }
        }

        // ---- name and state ----

        /// <summary>The card's own transform - what the pointer falls back to, so that focusing the
        /// card puts a mouse inside its rectangle the way a hover does.</summary>
        public virtual AgeTransform Root
        {
            get { return null; }
        }

        /// <summary>The label the planet's name is drawn in.</summary>
        public virtual AgePrimitiveLabel NameLabel
        {
            get { return null; }
        }

        /// <summary>The label the colonization state is drawn in. NULL means the prefab draws no such
        /// label and the state is read off the model instead
        /// (<see cref="PlanetStatusText"/>) - the empire card conveys the same fact by tinting the
        /// planet's NAME, which is not text to read.</summary>
        public virtual AgePrimitiveLabel StatusLabel
        {
            get { return null; }
        }

        /// <summary>The sentence behind the status label - what the game says about the state, and
        /// which technology would change it.</summary>
        public virtual AgeTooltip StatusTooltip
        {
            get { return null; }
        }

        /// <summary>The widget the game hangs the missing-technology hint on, where the state line
        /// carries one at all: the anchor of the Ctrl+Enter jump into the research tree.</summary>
        public virtual AgeTransform StatusHintWidget
        {
            get { return null; }
        }

        // ---- what kind of world ----

        /// <summary>The group the planet's type is drawn in.</summary>
        public virtual AgeTransform TypeGroup
        {
            get { return null; }
        }

        /// <summary>The label inside <see cref="TypeGroup"/>, which carries the type's own reference
        /// sentence on its tooltip (<c>RefreshPlanetBasicInfo</c> writes
        /// <c>guiElement.Description</c> there).</summary>
        public virtual AgePrimitiveLabel TypeLabel
        {
            get { return null; }
        }

        /// <summary>The label the planet's size is drawn in, where the prefab draws one.</summary>
        public virtual AgePrimitiveLabel SizeLabel
        {
            get { return null; }
        }

        /// <summary>The one label a prefab writes size AND type into as a single line, where it does
        /// that instead of drawing the two separately.</summary>
        public virtual AgePrimitiveLabel SizeAndTypeLabel
        {
            get { return null; }
        }

        /// <summary>The table of gameplay types - what living on this world is like.</summary>
        public virtual AgeTransform GameplayTypeTable
        {
            get { return null; }
        }

        // ---- the tables ----

        /// <summary>The anomalies found on this world.</summary>
        public virtual AgeTransform AnomaliesTable
        {
            get { return null; }
        }

        /// <summary>The curiosities still standing in orbit, drawn as a table of wired buttons.
        /// </summary>
        public virtual AgeTransform CuriositiesTable
        {
            get { return null; }
        }

        /// <summary>What the world is sitting on.</summary>
        public virtual AgeTransform DepositsGroup
        {
            get { return null; }
        }

        /// <summary>Whether this prefab's deposit item DRAWS its figure. Both of
        /// <c>ResourceDepositItem</c>'s labels are optional fields the refresh writes only when they
        /// are wired, so a prefab that wires neither draws a bare icon - and a figure read out there
        /// would be one nobody else can see.</summary>
        public virtual bool DepositsDrawAmount
        {
            get { return false; }
        }

        /// <summary>How worn out the world is - a mining probe's damage, or a Craver colony eating the
        /// planet it lives on.</summary>
        public virtual PlanetDepletionStatusItem Depletion
        {
            get { return null; }
        }

        // ---- the specialization improvement ----

        /// <summary>The box the card draws the world's specialization improvement in - the drawn-ness
        /// gate AND the dossier's anchor.</summary>
        public virtual AgeTransform ImprovementWidget
        {
            get { return null; }
        }

        /// <summary>The improvement's dossier, which the game keeps on a tooltip FIELD of its own
        /// rather than on the box, so nothing hanging off the card could ever find it.</summary>
        public virtual AgeTooltip ImprovementTooltip
        {
            get { return null; }
        }

        /// <summary>Whether the box WRITES the improvement's name, or draws only a picture and leaves
        /// the name to the wrapper on its tooltip.</summary>
        public virtual bool ImprovementDrawsWords
        {
            get { return false; }
        }

        // ---- the planet's own dossier ----

        /// <summary>The planet's own dossier.</summary>
        public virtual AgeTooltip PlanetTooltip
        {
            get { return null; }
        }

        /// <summary>Whether that dossier is the CARD's own tooltip section rather than a child of the
        /// Tooltips region - true on the map's orbital card, whose row is the thing a mouse resting on
        /// the planet hovers.</summary>
        public virtual bool PlanetTooltipIsCardSection
        {
            get { return false; }
        }

        // ---- the five outputs ----

        /// <summary>The strip of five output figures.</summary>
        public virtual FidsiEnumerator Fidsi
        {
            get { return null; }
        }

        /// <summary>The other strip - rating pips for a world nobody has settled. The card keeps both
        /// and swaps them, leaving the hidden one bound to whatever it last showed.</summary>
        public virtual AgeTransform FidsiScoreTable
        {
            get { return null; }
        }

        /// <summary>The element the rating pips are scaled against.</summary>
        public virtual FidsiParametersGuiElement FidsiParameters
        {
            get { return null; }
        }

        /// <summary>Whether the card is drawing the strip as NUMBERS rather than as rating pips -
        /// each prefab's own test, because each swaps the two strips on a different question.</summary>
        public virtual bool FidsiDrawsNumbers
        {
            get { return false; }
        }

        /// <summary>Whether the card is drawing the RATING pips - the other half of the same swap, and
        /// a question of its own because a prefab can hide both strips at once: the map's card hides
        /// everything on a world this empire has not surveyed, and pips read off one would rate a
        /// world the picture is drawing as an unknown. True is what the two cards that always draw
        /// their pip table answer.</summary>
        public virtual bool FidsiDrawsRatings
        {
            get { return true; }
        }

        /// <summary>The simulation object those numbers are read off, where they are drawn.</summary>
        public virtual Amplitude.Unity.Simulation.SimulationObject FidsiSource
        {
            get { return null; }
        }

        /// <summary>Whether the five figures get dossier children of their own (owner ruling per
        /// surface).</summary>
        public virtual bool DeclareFidsiDossiers
        {
            get { return false; }
        }

        // ---- the population ring(s) ----

        /// <summary>Whichever population ring the card is DRAWING - it decides the slot geometry.
        /// </summary>
        public virtual PlanetPopulationEnumerator RingMarkers
        {
            get { return null; }
        }

        /// <summary>The ring whose own <c>CanAcceptPopulationDrop</c> answers a drop.</summary>
        public virtual PlanetPopulationEnumerator RingTarget
        {
            get { return null; }
        }

        /// <summary>The Sanctuary's ring, which is both its geometry and its drop target.</summary>
        public virtual PlanetPopulationEnumerator GhostRingMarkers
        {
            get { return null; }
        }

        // ---- the Sanctuary band ----

        /// <summary>The band the card grows along its bottom while a ghost colony sits on this world.
        /// </summary>
        public virtual AgeTransform GhostGroup
        {
            get { return null; }
        }

        /// <summary>The band's own title, which is the band's line and carries its dossier.</summary>
        public virtual AgePrimitiveLabel GhostTitle
        {
            get { return null; }
        }

        /// <summary>The bare "3/5" the band draws beside a symbol.</summary>
        public virtual AgePrimitiveLabel GhostPopulationCount
        {
            get { return null; }
        }

        /// <summary>The Sanctuary's own output strip.</summary>
        public virtual FidsiEnumerator GhostFidsi
        {
            get { return null; }
        }

        /// <summary>The one thing the band can DO: turn one of its people into a sleeper.</summary>
        public virtual AgeTransform GhostTraitorButton
        {
            get { return null; }
        }

        // ---- the outpost band ----

        /// <summary>The band an outpost's card grows, and the gate on everything in it.</summary>
        public virtual AgeTransform OutpostGroup
        {
            get { return null; }
        }

        /// <summary>Who owns the outpost.</summary>
        public virtual AgePrimitiveLabel OutpostOwnerLabel
        {
            get { return null; }
        }

        /// <summary>The game's own sentence about how the outpost is getting on ("Colony in 24 Turn"),
        /// drawn on the card and so spoken rather than buffered.</summary>
        public virtual AgePrimitiveLabel OutpostBottomCaption
        {
            get { return null; }
        }

        /// <summary>When the next population unit arrives and which kind it will be.</summary>
        public virtual GrowthGaugeItem GrowthLine
        {
            get { return null; }
        }

        /// <summary>The strip of outpost actions along the top of the outpost band.</summary>
        public virtual AgeTransform OutpostActionsTable
        {
            get { return null; }
        }

        /// <summary>The decolonize tick under them.</summary>
        public virtual AgeControlToggle DecolonizeToggle
        {
            get { return null; }
        }

        /// <summary>The countdown the map's card draws instead of a growth line.</summary>
        public virtual AgePrimitiveLabel OutpostTimer
        {
            get { return null; }
        }

        /// <summary>What that countdown means.</summary>
        public virtual AgeTooltip OutpostTooltip
        {
            get { return null; }
        }

        // ---- the buttons every card shares ----

        public virtual AgeControlButton ColonizeButton
        {
            get { return null; }
        }

        public virtual AgeControlButton SpecializationButton
        {
            get { return null; }
        }

        public virtual AgeControlButton ReduceAnomalyButton
        {
            get { return null; }
        }

        public virtual AgeControlButton TerraformButton
        {
            get { return null; }
        }

        public virtual AgeTransform RenameButton
        {
            get { return null; }
        }

        // ---- the map card's own row of fleet-action buttons ----
        //
        // A prefab difference like any other: what a FLEET in the system could do to the world, which
        // only the map's card draws. Each is named after the fleet action it carries out
        // (<see cref="PlanetCardReader"/> composes them in the order the card draws them), and the
        // three in-progress ones after whatever is being done on the world right now.

        public virtual AgeControlButton VodyaniHintButton
        {
            get { return null; }
        }

        public virtual AgeControlButton UmbralChoirHintButton
        {
            get { return null; }
        }

        public virtual AgeControlButton BuyOutpostButton
        {
            get { return null; }
        }

        public virtual AgeControlButton MinorFactionButton
        {
            get { return null; }
        }

        public virtual AgeTransform PirateLairGroup
        {
            get { return null; }
        }

        public virtual AgeControlButton TerraformationButton
        {
            get { return null; }
        }

        public virtual AgeControlButton RestorationButton
        {
            get { return null; }
        }

        public virtual AgeControlButton AnomalyReductionButton
        {
            get { return null; }
        }

        public virtual AgeControlButton MiningProbeButton
        {
            get { return null; }
        }

        public virtual AgeControlButton DestroyButton
        {
            get { return null; }
        }

        public virtual AgeControlButton InProgressTerraformation
        {
            get { return null; }
        }

        public virtual AgeControlButton InProgressRestoration
        {
            get { return null; }
        }

        public virtual AgeControlButton InProgressAnomalyReduction
        {
            get { return null; }
        }

        // ---- the map card's wordless warning icons ----
        //
        // Each carries its sentence from the PREFAB whether or not the card is showing it, so the
        // reader asks the engine's own PAINTED test of each before it says a word.

        public virtual AgeTransform DecayIcon
        {
            get { return null; }
        }

        public virtual AgeTransform OutpostCancelIcon
        {
            get { return null; }
        }

        public virtual AgeTransform HauntIcon
        {
            get { return null; }
        }

        // ---- what the PAGE supplies ----

        /// <summary>Prefixes every id the card declares. The page's own namespace.</summary>
        public string Key;

        /// <summary>The scratch-carrier namespace the card's ring slots park their dossiers under -
        /// the page's, because two pages can draw a ring over the same world.</summary>
        public string RingScratch;

        /// <summary>The card's own click, where the page has one. Null makes the card a plain group
        /// rather than a button.</summary>
        public Action OnActivate;

        /// <summary>Whether a population unit may be picked up off this card's rings at all - there is
        /// a carry only where the page offers somewhere to put one down.</summary>
        public Func<bool> CanCarry;

        /// <summary>Whether the world's ring would take what is being carried, right now.</summary>
        public Func<CarryItem, bool> Accepts;

        /// <summary>The page's own drop onto the world's ring. The second argument is the affinity
        /// standing in the slot, which is the one the game's SWAP sends back the other way.</summary>
        public Func<CarryItem, StaticString, DropResult> Drop;

        /// <summary>The same pair for the SANCTUARY's ring. A card draws up to two rings over one
        /// world and the game runs both through one drag client, but they land on different colonies,
        /// so the page answers for each separately.</summary>
        public Func<CarryItem, bool> GhostAccepts;

        public Func<CarryItem, StaticString, DropResult> GhostDrop;

        /// <summary>The page's carrier for the Nth deposit dossier the game is NOT drawing an item
        /// for, where the page keeps one. Null everywhere the card's own table is the whole
        /// answer.</summary>
        public Func<int, AgeTooltip> DepositCarrier;

        /// <summary>Anything the page hangs off the card that is the PAGE's rather than the card's -
        /// the map's quest pins. Emitted last, inside the card's own group.</summary>
        public Action<GraphBuilder> AppendChildren;
    }
}
