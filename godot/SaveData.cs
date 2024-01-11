using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCZ.Core;

/// <summary>
/// Just using an in-memory singleton for now. Will refactor when needed.
/// </summary>
static class SaveData
{
    /// <summary>
    /// Persisted data; do not change values.
    /// It is important that Gold > Silver and so on, because we credit the user with Max(previous, current).
    /// </summary>
    public enum LevelCompletion
    {
        Incomplete = 0,
        Complete = 1,
        Bronze = 2,
        Silver = 3,
        Gold = 4,
    };

    sealed class LevelsData
    {
        public const string FilePath = "user://LevelsData.txt";

        public Dictionary<int, LevelCompletion> Completion { get; set; } = new Dictionary<int, LevelCompletion>();
    }

    sealed class ScoreAttackData
    {
        public const string FilePath = "user://ScoreAttackData.txt";

        public sealed class Details
        {
            public int PB { get; set; }
        }

        public Details Tall { get; set; } = new();
        public Details Wide { get; set; } = new();

        public Details GetDetails(Layout layout)
        {
            switch (layout)
            {
                case Layout.Tall: return Tall;
                case Layout.Wide: return Wide;
                default: throw new ArgumentException($"Assert fail - unexpected layout {layout}");
            }
        }
    }

    private static LevelsData? _levelsData = null;
    private static ScoreAttackData? _scoreAttackData = null;

    public static LevelCompletion GetCompletion(int level)
    {
        GetLevelsData().Completion.TryGetValue(level, out var value);
        return value;
    }

    private static T? ReadFile<T>(string path) where T : class
    {
        using var file = new Godot.File();
        var error = file.Open(path, Godot.File.ModeFlags.Read);
        if (error == Godot.Error.Ok)
        {
            string filePath = file.GetPathAbsolute();
            string content = file.GetAsText();
            file.Close();
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not deserialize {typeof(T).FullName} from {filePath}");
                Console.Error.WriteLine(ex.ToString());
            }
        }
        return null;
    }

    private static LevelsData GetLevelsData()
    {
        _levelsData = _levelsData ?? ReadFile<LevelsData>(LevelsData.FilePath) ?? new LevelsData();
        return _levelsData;
    }

    private static ScoreAttackData GetScoreAttackData()
    {
        _scoreAttackData = _scoreAttackData ?? ReadFile<ScoreAttackData>(ScoreAttackData.FilePath) ?? new ScoreAttackData();
        return _scoreAttackData;
    }

    private static void SaveToFile(object data, string path)
    {
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        using var file = new Godot.File();
        var error = file.Open(path, Godot.File.ModeFlags.Write);
        if (error != Godot.Error.Ok)
        {
            Console.Error.WriteLine($"Could not save to {file.GetPathAbsolute()}, error {error}");
            return;
        }
        file.StoreString(json);
        file.Close();
    }

    private static void SaveLevelsData(LevelsData data)
    {
        _levelsData = data;
        SaveToFile(_levelsData, LevelsData.FilePath);
    }

    private static void SaveScoreAttackData(ScoreAttackData data)
    {
        _scoreAttackData = data;
        SaveToFile(_scoreAttackData, ScoreAttackData.FilePath);
    }

    public static void RecordLevelComplete(int level)
    {
        MaybeUpdateLevelsData(level, LevelCompletion.Complete);
    }

    public static void RecordMedal(int level, MedalKind medal)
    {
        MaybeUpdateLevelsData(level, ConvertMedal(medal));
    }

    private static void MaybeUpdateLevelsData(int level, LevelCompletion completion)
    {
        var data = GetLevelsData();
        data.Completion.TryGetValue(level, out var existing);
        if (completion > existing)
        {
            data.Completion[level] = completion;
            SaveLevelsData(data);
        }
    }

    private static LevelCompletion ConvertMedal(MedalKind medal)
    {
        switch (medal)
        {
            case MedalKind.Gold: return LevelCompletion.Gold;
            case MedalKind.Silver: return LevelCompletion.Silver;
            case MedalKind.Bronze: return LevelCompletion.Bronze;
            default: return LevelCompletion.Incomplete;
        }
    }

    public static int GetPersonalBest(Layout layout)
    {
        var data = GetScoreAttackData();
        var details = data.GetDetails(layout);
        return details.PB;
    }

    /// <summary>
    /// Returns true if the given <paramref name="score"/> is a new PB, false otherwise.
    /// Updates save data if necessary.
    /// </summary>
    public static bool UpdatePersonalBest(Layout layout, int score)
    {
        var data = GetScoreAttackData();
        var details = data.GetDetails(layout);
        if (score <= details.PB)
        {
            return false;
        }
        details.PB = score;
        SaveScoreAttackData(data);
        return true;
    }
}
