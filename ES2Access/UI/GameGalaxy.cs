using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace ES2Access.UI
{
    /// <summary>
    /// The galaxy model, reached the one way that compiles and runs on BOTH stores' builds of the
    /// game - and the only place in the mod allowed to touch it.
    ///
    /// The class holding the map is called <c>Galaxy</c> in the Steam build and
    /// <c>GalaxyIngame</c> in the GOG one: GOG ships the GOG Galaxy SDK
    /// (<c>GalaxyCSharp.dll</c>, namespace <c>Galaxy.Api</c>), whose namespace collides with the
    /// game's own top-level type, so Amplitude renamed the class for that build. The property is
    /// <c>Game.Galaxy</c> on both - only its declared TYPE moved.
    ///
    /// Naming either type statically breaks the other store in a different way, and neither
    /// failure is recoverable at the call site: <c>Galaxy</c> will not compile against the GOG
    /// assemblies, and a binary compiled naming <c>GalaxyIngame</c> carries that type in the member
    /// reference for <c>Game.get_Galaxy()</c> and so fails at RUNTIME on Steam. One reflected read
    /// of the property, with everything downstream statically typed, is what lets one built DLL
    /// serve both - <c>GameNode</c>, <c>StarSystemNode</c> and <c>SpecialNode</c> are top-level and
    /// identically named in both builds, and the two properties read here have the same names and
    /// shapes in both (<c>GameNode[] GameNodes</c>, <c>IEnumerable&lt;StarSystemNode&gt;
    /// StarSystemNodes</c>). Keep the seam here: a second site naming the type re-opens the split.
    ///
    /// The reflected members are cached in statics with no invalidation, which is sound because a
    /// process loads exactly one build of the game. They are <c>PropertyInfo</c>s and nothing else -
    /// no game object is held here, so a hot reload needs no teardown.
    /// </summary>
    public static class GameGalaxy
    {
        private static PropertyInfo _galaxyOfGame;
        private static PropertyInfo _starSystemNodes;
        private static PropertyInfo _gameNodes;

        /// <summary>
        /// Whether there is a galaxy to read at all - the "no game running" test the callers that do
        /// other work alongside their node walk gate on.
        /// </summary>
        public static bool Present()
        {
            return Instance() != null;
        }

        /// <summary>
        /// Every star system node on the map, empty while there is no game.
        /// </summary>
        public static IEnumerable<StarSystemNode> StarSystemNodes()
        {
            IEnumerable nodes = Read(ref _starSystemNodes, "StarSystemNodes") as IEnumerable;
            if (nodes == null)
            {
                yield break;
            }

            foreach (object node in nodes)
            {
                StarSystemNode system = node as StarSystemNode;
                if (system != null)
                {
                    yield return system;
                }
            }
        }

        /// <summary>
        /// Every node on the map, star system or not; null while there is no game.
        /// </summary>
        public static GameNode[] GameNodes()
        {
            return Read(ref _gameNodes, "GameNodes") as GameNode[];
        }

        private static object Instance()
        {
            Game game = Gui.Game;
            if (game == null)
            {
                return null;
            }

            if (_galaxyOfGame == null)
            {
                _galaxyOfGame = typeof(Game).GetProperty("Galaxy");
                if (_galaxyOfGame == null)
                {
                    return null;
                }
            }

            return _galaxyOfGame.GetValue(game, null);
        }

        private static object Read(ref PropertyInfo cache, string name)
        {
            object galaxy = Instance();
            if (galaxy == null)
            {
                return null;
            }

            if (cache == null)
            {
                cache = galaxy.GetType().GetProperty(name);
                if (cache == null)
                {
                    return null;
                }
            }

            return cache.GetValue(galaxy, null);
        }
    }
}
