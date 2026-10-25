// A dependency-free test runner: any public static void method marked [Test] in this
// assembly is run; a thrown exception is a failure. Exit code 1 if anything failed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Rigfall.Core;

namespace Rigfall.CoreTests
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class TestAttribute : Attribute { }

    public static class Assert
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception(message + " (expected " + expected + ", got " + actual + ")");
        }

        public static void Near(float expected, float actual, float tolerance, string message)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception(message + " (expected " + expected + " ± " + tolerance + ", got " + actual + ")");
        }
    }

    /// <summary>Loads the real JSON game data with System.Text.Json (Unity uses JsonUtility; both map by field name).</summary>
    public static class Data
    {
        static GameDatabase cached;

        public static string DataDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "Rigfall", "Resources", "Data");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("Could not find Assets/Rigfall/Resources/Data above " + AppContext.BaseDirectory);
        }

        static T Load<T>(string name)
        {
            var opts = new JsonSerializerOptions { IncludeFields = true };
            return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(DataDir(), name + ".json")), opts);
        }

        public static GameDatabase Db
        {
            get
            {
                if (cached == null)
                    cached = new GameDatabase(Load<RarityList>("rarities"), Load<DifficultyList>("difficulties"),
                        Load<ClassList>("classes"), Load<WeaponList>("weapons"), Load<PerkList>("perks"),
                        Load<MutatorList>("mutators"), Load<EnemyList>("enemies"), Load<UnitList>("units"),
                        Load<BuildingList>("buildings"), Load<MapList>("maps"), Load<AchievementList>("achievements"),
                        Load<QuestList>("quests"), Load<WheelList>("wheel"), Load<TutorialList>("tutorial"),
                        Load<GlossaryList>("glossary"), Load<MusicList>("music"));
                return cached;
            }
        }
    }

    public static class Program
    {
        public static int Main()
        {
            var tests = typeof(Program).Assembly.GetTypes()
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
                .OrderBy(m => m.DeclaringType.Name).ThenBy(m => m.Name)
                .ToList();
            int failed = 0;
            foreach (var t in tests)
            {
                try
                {
                    t.Invoke(null, null);
                    Console.WriteLine("  ok   " + t.DeclaringType.Name + "." + t.Name);
                }
                catch (TargetInvocationException e)
                {
                    failed++;
                    Console.WriteLine("  FAIL " + t.DeclaringType.Name + "." + t.Name + ": " + e.InnerException.Message);
                }
            }
            Console.WriteLine();
            Console.WriteLine((tests.Count - failed) + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
