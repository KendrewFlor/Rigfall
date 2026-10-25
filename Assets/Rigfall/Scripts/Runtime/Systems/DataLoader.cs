// Reads every JSON file in Resources/Data with Unity's JsonUtility and builds the
// GameDatabase used by the rest of the game.
using Rigfall.Core;
using UnityEngine;

namespace Rigfall.Runtime
{
    public static class DataLoader
    {
        static T Load<T>(string name) where T : new()
        {
            var asset = Resources.Load<TextAsset>("Data/" + name);
            if (asset == null)
            {
                Debug.LogError("Rigfall: missing data file Resources/Data/" + name + ".json");
                return new T();
            }
            try { return JsonUtility.FromJson<T>(asset.text); }
            catch (System.Exception e)
            {
                Debug.LogError("Rigfall: could not parse " + name + ".json: " + e.Message);
                return new T();
            }
        }

        public static GameDatabase LoadAll()
        {
            return new GameDatabase(
                Load<RarityList>("rarities"), Load<DifficultyList>("difficulties"), Load<ClassList>("classes"),
                Load<WeaponList>("weapons"), Load<PerkList>("perks"), Load<MutatorList>("mutators"),
                Load<EnemyList>("enemies"), Load<UnitList>("units"), Load<BuildingList>("buildings"),
                Load<MapList>("maps"), Load<AchievementList>("achievements"), Load<QuestList>("quests"),
                Load<WheelList>("wheel"), Load<TutorialList>("tutorial"), Load<GlossaryList>("glossary"),
                Load<MusicList>("music"));
        }
    }
}
