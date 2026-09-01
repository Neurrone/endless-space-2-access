using System;
using System.Collections.Generic;
using Amplitude.Unity.Framework;
using ES2Access.Core.Speech;
using ES2Access.Core.UI;
using ES2Access.Core.UI.Graph;
using ES2Access.Core.Util;
using ES2Access.UI;

namespace ES2Access.Screens
{
    /// <summary>The empire's trade routes, heard on the places the lens draws them across.</summary>
    public sealed partial class GalaxyHudScreen
    {
        /// <summary>The routes the lens is drawing, tied to the places and lanes they run over
        /// (<see cref="Core.UI.TradeWeave"/>). Reused rather than reallocated: the whole walk is redone
        /// on every build, and on most maps it holds nothing at all.</summary>
        private readonly TradeWeave _weave = new TradeWeave();

        /// <summary>
        /// Read the empire's routes off the trade model, once per build.
        ///
        /// GATED ON THE MODE AND NOTHING ELSE, because that is how the drawing is gated: the renderer
        /// computes its lines when the scan view is switched ON and asks nothing about zoom, lens or
        /// camera (<c>TradeRouteRenderer.ViewService_ScanViewSwitched</c> :184-190 into
        /// <c>UpdatePlayerEmpireDependantData</c> :204-300, which has no band term in it) - so every
        /// lens that draws the map draws the routes, and the weave rides every band the systems and
        /// lanes are on.
        ///
        /// From the MODEL rather than from the renderer: the renderer computes its lines once on the
        /// way in and never again, so a blockade that lands while the lens is up leaves the picture
        /// stale, and reading the model keeps the words current.
        ///
        /// Cleared whether or not the mode is up, so that leaving it cannot leave a lane in the
        /// ordinary map still talking about a line nobody is drawing.
        /// </summary>
        private void GatherTradeRoutes(Empire empire)
        {
            _weave.Clear();
            try
            {
                DepartmentOfCommerce commerce =
                    !Scanning || empire == null ? null : empire.GetAgency<DepartmentOfCommerce>();
                if (commerce == null)
                {
                    return;
                }

                IList<TradingCompany> companies = commerce.TradingCompanies;
                for (int i = 0; companies != null && i < companies.Count; i++)
                {
                    TradingCompany company = companies[i];
                    if (company == null)
                    {
                        continue;
                    }

                    // The two lists the game's own EnumerableTradingRoutes yields in turn, walked by
                    // index: the enumerable allocates an iterator, and this runs every build.
                    TakeRoutes(company.TradingRoutes);
                    TakeRoutes(company.ExternalTradingRoutes);
                }
            }
            catch (Exception e)
            {
                Log.Warn("scan: reading the trade routes threw: " + e);
            }
        }

        /// <summary>Every route in one of a company's two lists, taken the way the renderer takes it
        /// (<c>TradeRouteRenderer</c> :225-247): the path pairwise, the blockade flag accumulating
        /// along it, and the whole-route flag a blockade at either END sets before the first leg.
        /// </summary>
        private void TakeRoutes(IList<TradingRoute> routes)
        {
            for (int i = 0; routes != null && i < routes.Count; i++)
            {
                TradingRoute route = routes[i];
                NodePosition[] path = route == null ? null : route.Path;
                if (path == null || path.Length < 2)
                {
                    continue;
                }

                int[] nodes = new int[path.Length];
                for (int j = 0; j < path.Length; j++)
                {
                    nodes[j] = path[j].NodeIndex;
                }

                TradingRouteBlockade blockade = route.Blockade;
                TradingRouteBlockadedNodeInfo[] info = blockade == null ? null : blockade.NodeInfo;
                bool[] blockaded = null;
                if (info != null)
                {
                    blockaded = new bool[info.Length];
                    for (int j = 0; j < info.Length; j++)
                    {
                        blockaded[j] = info[j] != null && info[j].IsBeingSoftBlockaded;
                    }
                }

                _weave.Add(
                    nodes,
                    blockade != null
                        && (
                            blockade.IsBeingSoftBlockadedOnHQ
                            || blockade.IsBeingSoftBlockadedOnSubsidiary
                        ),
                    blockaded
                );
            }
        }

        /// <summary>
        /// What the routes make of one system, said on the system's own row: one line per route
        /// (RULED 2026-09-01 - never a merged summary), naming where the route goes for a system at
        /// either end of it, and both ends for a system it merely crosses.
        ///
        /// Nothing at all where no route touches the place, which is every system on a map with no
        /// trading company.
        /// </summary>
        private void WeaveInto(NodeVtable vtable, StarSystemNode node, Empire empire)
        {
            IList<TradeWeave.Tie> ties = _weave.Count == 0
                ? null
                : _weave.At(node.NodePosition.NodeIndex);
            for (int i = 0; ties != null && i < ties.Count; i++)
            {
                TradeWeave.Tie tie = ties[i];
                TradeWeave.Route route = _weave[tie.Route];
                Empire looking = empire;
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(
                        () =>
                            tie.Endpoint
                                ? TradeWeave.EndText(PlaceName(tie.Other, looking), tie.Blockaded)
                                : TradeWeave.ThroughText(
                                    PlaceName(route.Start, looking),
                                    PlaceName(route.End, looking)
                                ),
                        false
                    )
                );
            }
        }

        /// <summary>
        /// What the routes make of one star lane, said on the lane's own row: one line per route
        /// riding it, each carrying the colour the renderer paints THAT LINE with - so a lane carrying
        /// an open route and a blockaded one says "mixed" on both, which is the one line the player is
        /// looking at (<see cref="Core.UI.TradeWeave"/>).
        /// </summary>
        private void WeaveAlong(NodeVtable vtable, StarSystemNode node, GameNode far, Empire empire)
        {
            IList<TradeWeave.Ride> rides = _weave.Count == 0 || far == null
                ? null
                : _weave.On(node.NodePosition.NodeIndex, far.NodePosition.NodeIndex);
            for (int i = 0; rides != null && i < rides.Count; i++)
            {
                TradeWeave.Ride ride = rides[i];
                TradeWeave.Route route = _weave[ride.Route];
                Empire looking = empire;
                vtable.Announcements.Add(
                    GraphNodes.ValuePart(
                        () =>
                            TradeWeave.LaneText(
                                PlaceName(route.Start, looking),
                                PlaceName(route.End, looking),
                                ride.State
                            ),
                        false
                    )
                );
            }
        }

        /// <summary>
        /// What a place on a route is CALLED, by the same rule everything else on this map is named by:
        /// the game's own name where the player has explored the place, and the mod's word for an
        /// unexplored star otherwise.
        ///
        /// The guard is nearly always moot - creating a route reveals every node on its path to the
        /// empire that owns it (<c>DepartmentOfCommerce.RevealNodesOnTradingRoutePath</c> :1943-1959,
        /// which raises each node to Known and reveals the space around it) - but revealing a node is
        /// not the same threshold as having explored it, and the lens itself writes "???" over a system
        /// below that threshold. So the route never leaks a name the label is withholding.
        ///
        /// A node index the positioning service cannot place says nothing at all, which silences the
        /// whole line rather than reading a number out.
        /// </summary>
        private static string PlaceName(int index, Empire empire)
        {
            try
            {
                IPositioningService positioning = Services.GetService<IPositioningService>();
                GameNode node =
                    positioning == null ? null : positioning.GetGameNode(new NodePosition(index));
                if (node == null)
                {
                    return null;
                }

                return Perceived(node, empire)
                    ? node.LocalizedName
                    : ModStrings.Get(ModStrings.GalaxySystemUnexplored);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
