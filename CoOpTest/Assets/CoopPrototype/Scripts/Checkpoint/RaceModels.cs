using UnityEngine;

namespace CoopPrototype.Checkpoint
{
    /// <summary>Local, visual-only traveller figures (in vehicles and in the holding cell). Never networked or collidable.</summary>
    public static class RaceModels
    {
        static MaterialPropertyBlock block;
        public static GameObject Spawn(RaceDefinition race, Color skin, Transform parent, string name)
        {
            if (race == null || race.model == null || parent == null) return null;
            var figure = Object.Instantiate(race.model, parent, false);
            figure.name = name;
            foreach (var collider in figure.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            Tint(figure, skin);
            return figure;
        }
        public static void Tint(GameObject figure, Color skin)
        {
            block ??= new MaterialPropertyBlock();
            block.SetColor("_BaseColor", skin);
            foreach (var renderer in figure.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.StartsWith("Skin")) renderer.SetPropertyBlock(block);
        }
        /// <summary>Race of a traveller, falling back to the codex's first race so older data still shows a figure.</summary>
        public static RaceDefinition RaceOf(TravellerDefinition traveller, CheckpointCodex codex)
        {
            if (traveller != null && traveller.raceInfo != null) return traveller.raceInfo;
            return codex != null && codex.races != null && codex.races.Length > 0 ? codex.races[0] : null;
        }
    }
}
