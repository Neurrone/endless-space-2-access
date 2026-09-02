using System;
using Amplitude.Unity.Framework;
using UnityEngine;

namespace ES2Access.UI
{
    /// <summary>
    /// The OBJECT the map is drawing for a thing in the galaxy - the bridge from a game entity to the
    /// scene.
    ///
    /// Nearly everything the map draws is a Unity object the galaxy view builds per entity and hangs
    /// the interesting components off: the node a star is drawn as, the sphere a planet is drawn as,
    /// the cursor targets a click is dispatched through. The game keeps them in one registry keyed by
    /// entity id (<c>IGalaxyEntityFactoryService</c>), and the only way to reach one is to ask that
    /// registry and then ask the object for the component wanted.
    ///
    /// Two lines, and four places in the mod wrote both of them - each with its own service lookup,
    /// its own null ladder and its own try/catch. Here once: an entity nobody is drawing, a service
    /// that is not up yet (between games, mid-load) and a component the object does not carry are all
    /// the same answer, which is nothing.
    /// </summary>
    public static class GalaxyEntities
    {
        /// <summary>The component the map's object for <paramref name="entity"/> carries, or null
        /// where the map is drawing no object for it or the object carries no such component.
        /// </summary>
        public static T Component<T>(GameEntityGUID entity)
            where T : Component
        {
            try
            {
                GameObject drawn = Drawn(entity);
                return drawn == null ? null : drawn.GetComponent<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The same for a thing the map draws SEVERAL of on one object - a starlane, which
        /// carries one cursor target per end. Never null: no object and no components are the same
        /// empty answer, so a caller may index the length without testing first.</summary>
        public static T[] Components<T>(GameEntityGUID entity)
            where T : Component
        {
            try
            {
                GameObject drawn = Drawn(entity);
                T[] found = drawn == null ? null : drawn.GetComponents<T>();
                return found ?? new T[0];
            }
            catch (Exception)
            {
                return new T[0];
            }
        }

        private static GameObject Drawn(GameEntityGUID entity)
        {
            IGalaxyEntityFactoryService entities =
                Services.GetService<IGalaxyEntityFactoryService>();
            return entities == null ? null : entities[entity];
        }
    }
}
