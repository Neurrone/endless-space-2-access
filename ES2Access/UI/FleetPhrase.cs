using System;
using System.Collections.Generic;
using ES2Access.Core.Speech;
using ES2Access.Core.Util;

namespace ES2Access.UI
{
    /// <summary>
    /// The one sentence the mod says about a single fleet, wherever a single fleet is named - the
    /// galaxy tree's fleet row, an inspected cell, a scanner result, and every turn-log line whose
    /// subject is a fleet. One phrase in one order, so a fleet heard on the map and the same fleet
    /// heard out of the log are recognisably the same fleet.
    ///
    /// The order is name, standing and owner, hero, ships, and every part is left OUT rather than
    /// filled with a word about not knowing:
    /// <list type="bullet">
    /// <item>NAME is the fleet's own <c>LocalizedName</c>, and belongs to whoever is speaking - the
    /// tree's row already carries it as its label, so only <see cref="Full"/> puts it back.</item>
    /// <item>STANDING AND OWNER is somebody else's fleet only; the player's own fleets are simply
    /// theirs and say nothing. Whose it is is the game's own disguise rule
    /// (<see cref="DisplayedOwner"/>) and which way the player stands to them is the mod's one
    /// standing ladder (<see cref="FleetPresence.SideOf(Empire)"/>) - so a cold-war neighbour's
    /// fleets read "enemy", in the game's own word. What that owner is CALLED is the mod's one
    /// naming answer (<see cref="EmpireNames.Named"/>), so a pirate fleet reads "enemy Pirates" and
    /// an unmet major's "enemy Unknown Empire".</item>
    /// <item>HERO is any fleet's, own or foreign: the game's own fleet dossier draws a foreign
    /// hero's name with no ownership gate on it at all
    /// (<c>PanelFeatureGarrisonInfo.RefreshFleetInformation</c> :150-156). The name only - the level
    /// the game's tooltip prints beside it is owner-ruled out.</item>
    /// <item>SHIPS is what the fleet is made of, and only where the map itself would put a number on
    /// the lozenge (<see cref="FleetPresence.ShowsShipCount"/>) - below that permission the whole
    /// part goes, because the picture shows no placeholder either.</item>
    /// </list>
    ///
    /// The ship groups are counted by ship DESIGN, in the order the picture would be read: the group
    /// holding the ship the map actually draws for this fleet first
    /// (<c>Fleet.GetMostVisuallyImportantShip</c>, the very call the map's own prefab resolver makes),
    /// then the rest alphabetically so the same fleet reads the same way round twice running. The
    /// hero's own ship is grouped and counted like any other and is not marked out (owner ruling
    /// 2026-08-26) - that a hero is aboard has already been said. An
    /// AUTOMATED fleet has no design name worth saying - its <c>LocalizedName</c> is a raw key
    /// ("ShipDesignAutomatedShipTerrans") - so it is named with the two words the game's own automated
    /// dossier draws instead, the ship's SIZE and ROLE titles
    /// (<c>PanelFeatureGarrisonInfoAutomatedFleet.Bind</c> :62-73): "Small Logistics".
    ///
    /// A group of one carries no number - the design's name is the whole of it - and only two or more
    /// are counted ("2 Escort"), by the owner's ruling of 2026-08-26.
    ///
    /// Beside its data rather than in <c>Core/</c>: every answer here is read off live game objects.
    /// </summary>
    public static class FleetPhrase
    {
        /// <summary>The whole phrase with the fleet's own name in front of it - what a surface says
        /// when it is naming the fleet from scratch (a turn-log sentence). Never null for a live
        /// fleet: the name alone is still a phrase.
        ///
        /// <paramref name="withOwner"/> false leaves the standing and owner out and keeps the rest -
        /// for the one sentence that has already named the owner in a slot of its own (the sighting
        /// notification's "The enemy Leaper (AI) fleet ..."), where saying it twice would be the mod
        /// stammering.</summary>
        public static string Full(Fleet fleet, bool withOwner = true)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.Fragment(fleet.LocalizedName);
                message.ListItemForcedComma(withOwner ? Describe(fleet) : Aboard(fleet));
                return message.Build();
            }
            catch (Exception)
            {
                return fleet == null ? null : fleet.LocalizedName;
            }
        }

        /// <summary>Everything the phrase says about a fleet EXCEPT its name - for the surfaces whose
        /// row, cell or result already carries the name. Null where a fleet of the player's own has no
        /// hero and the ship part is unavailable.</summary>
        public static string Describe(Fleet fleet)
        {
            try
            {
                MessageBuilder message = new MessageBuilder();
                message.ListItem(Owned(fleet));
                message.ListItem(Aboard(fleet));
                return message.Build();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Who is riding with the fleet and what it is made of - everything
        /// <see cref="Describe"/> says once whose it is has been settled.</summary>
        private static string Aboard(Fleet fleet)
        {
            MessageBuilder message = new MessageBuilder();
            message.ListItem(Commander(fleet));
            message.ListItem(Composition(fleet));
            return message.Build();
        }

        /// <summary>Whose fleet this is and which way the player stands to them - "enemy Leaper (AI)".
        /// Null for the player's own, which are theirs and need no saying.</summary>
        public static string Owned(Fleet fleet)
        {
            return Owned(DisplayedOwner(fleet));
        }

        /// <summary>The same for an empire named directly - what the turn log has when the fleet
        /// itself has gone out of sight and only the remembered owner is left to speak of.</summary>
        public static string Owned(Amplitude.Unity.Game.Empire owner)
        {
            try
            {
                Empire named = owner as Empire;
                if (named == null)
                {
                    return null;
                }

                string key;
                switch (FleetPresence.SideOf(named))
                {
                    case FleetPresence.Side.Player:
                        return null;
                    case FleetPresence.Side.Enemy:
                        key = ModStrings.FleetOwnedEnemy;
                        break;
                    case FleetPresence.Side.Friendly:
                        key = ModStrings.FleetOwnedFriendly;
                        break;
                    default:
                        key = ModStrings.FleetOwnedNeutral;
                        break;
                }

                string name = EmpireNames.Named(named);
                return string.IsNullOrEmpty(name) ? null : ModStrings.Format(key, name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The hero riding with this fleet, named - "hero Hadri Lenko". Null where there is
        /// none.</summary>
        public static string Commander(Fleet fleet)
        {
            string name = HeroName(fleet);
            return name == null ? null : ModStrings.Format(ModStrings.FleetHero, name);
        }

        /// <summary>The hero's bare name, for the one sentence that puts it in a frame of its own (the
        /// sighting notification's body).</summary>
        public static string HeroName(Fleet fleet)
        {
            try
            {
                Hero hero = fleet == null ? null : fleet.AssignedHero;
                if (hero == null)
                {
                    return null;
                }

                string name = AgeText.Clean(hero.LocalizedName);
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>What the fleet is made of, grouped by design and ordered picture-first - "Flag,
        /// Corvette, 2 Escort". Null where the map would put no number on the lozenge.
        ///
        /// Memoised per fleet on what can change the answer - how many ships it holds, which hero
        /// ship is aboard, whose it looks like and whether the player may count it - because a
        /// galaxy-tree announcement part is ASKED on every frame whether anything is watching it or
        /// not, and this one walks the fleet's ships and asks the simulation which one the map draws.
        /// </summary>
        public static string Composition(Fleet fleet)
        {
            try
            {
                if (fleet == null)
                {
                    return null;
                }

                bool shows = FleetPresence.ShowsShipCount(fleet);
                ulong key = fleet.GUID.ToUInt64(null);
                Memo memo;
                if (
                    _memos.TryGetValue(key, out memo)
                    && memo.Ships == fleet.ShipsIncludingHeroCount
                    && ReferenceEquals(memo.HeroShip, fleet.HeroShip)
                    && ReferenceEquals(memo.Owner, DisplayedOwner(fleet))
                    && memo.Shows == shows
                )
                {
                    return memo.Text;
                }

                // A fleet is a short-lived thing and the map draws a handful at a time; a cap keeps
                // a long game from remembering every fleet that ever flew.
                if (_memos.Count > 256)
                {
                    _memos.Clear();
                }

                memo = new Memo
                {
                    Ships = fleet.ShipsIncludingHeroCount,
                    HeroShip = fleet.HeroShip,
                    Owner = DisplayedOwner(fleet),
                    Shows = shows,
                    Text = shows ? Build(fleet) : null,
                };
                _memos[key] = memo;
                _builds++;
                return memo.Text;
            }
            catch (Exception e)
            {
                Log.Warn("galaxy: reading what a fleet is made of threw: " + e);
                return null;
            }
        }

        /// <summary>Whose the fleet LOOKS like - the game's own question
        /// (<c>GuiFleetGroup.Empire</c>): the player's own fleets are theirs, and everybody else's is
        /// the empire it is flying the colours of, so a disguised fleet reads as whoever it is
        /// pretending to be until the disguise is seen through.</summary>
        public static Empire DisplayedOwner(Fleet fleet)
        {
            try
            {
                if (fleet == null)
                {
                    return null;
                }

                Empire player = Gui.PlayerEmpire;
                return ReferenceEquals(fleet.Empire, player) ? fleet.Empire : fleet.DisplayedEmpire;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>How many times the ship walk has actually run, for a memo-cost probe: a part
        /// asked on every frame that answers off the memo leaves this standing still.</summary>
        internal static int Builds
        {
            get { return _builds; }
        }

        private sealed class Memo
        {
            public int Ships;
            public HeroShip HeroShip;
            public Empire Owner;
            public bool Shows;
            public string Text;
        }

        private sealed class Group
        {
            public string Name;
            public int Count;
        }

        private static readonly Dictionary<ulong, Memo> _memos = new Dictionary<ulong, Memo>();
        private static int _builds;

        private static string Build(Fleet fleet)
        {
            bool automated = fleet.IsAutomated;
            Ship drawn = fleet.GetMostVisuallyImportantShip();
            List<Group> groups = new List<Group>();
            string first = null;

            foreach (Ship ship in fleet.ShipsIncludingHero)
            {
                string name = GroupName(ship, automated);
                if (name == null)
                {
                    continue;
                }

                Group group = null;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (groups[i].Name == name)
                    {
                        group = groups[i];
                        break;
                    }
                }

                if (group == null)
                {
                    group = new Group { Name = name };
                    groups.Add(group);
                }

                group.Count++;
                if (ReferenceEquals(ship, drawn))
                {
                    first = name;
                }
            }

            string lead = first;
            groups.Sort(
                delegate(Group a, Group b)
                {
                    bool one = a.Name == lead;
                    bool two = b.Name == lead;
                    if (one != two)
                    {
                        return one ? -1 : 1;
                    }

                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                }
            );

            MessageBuilder message = new MessageBuilder();
            for (int i = 0; i < groups.Count; i++)
            {
                // A group of one is the ship's own name and nothing else: "a Flag" is what the
                // picture shows, and "1 Flag" is a count of a thing there is only one of (owner
                // ruling 2026-08-26). Only two or more earn the counted form.
                message.ListItem(
                    groups[i].Count > 1
                        ? ModStrings.Format(
                            ModStrings.FleetShipGroup,
                            groups[i].Count,
                            groups[i].Name
                        )
                        : groups[i].Name
                );
            }

            return message.Build();
        }

        /// <summary>What to call a ship's kind. Its design's name, except on an automated fleet, whose
        /// designs are named by a raw key the player has never seen - there the game's own dossier
        /// draws the ship's size and role titles instead, and so does this.</summary>
        private static string GroupName(Ship ship, bool automated)
        {
            if (ship == null)
            {
                return null;
            }

            if (!automated)
            {
                return ship.ShipDesign == null ? null : ship.ShipDesign.LocalizedName;
            }

            string size = AgeText.Clean(
                Gui.GetTitle(ship.GetDescriptorNameFromType(Ship.ShipSizeDescriptorType))
            );
            string role =
                ship.ShipDesign == null || ship.ShipDesign.Role == null
                    ? null
                    : AgeText.Clean(Gui.GetTitle(ship.ShipDesign.Role.Name));
            return new MessageBuilder().Fragment(size).Fragment(role).Build();
        }
    }
}
