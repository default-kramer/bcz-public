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

    class LevelsData
    {
        public const string FilePath = "user://LevelsData.txt";

        public Dictionary<int, LevelCompletion> Completion { get; set; } = new Dictionary<int, LevelCompletion>();
    }

    public static int ScoreAttackPB { get; set; } = 0;

    private static LevelsData? _levelsData = null;

    public static LevelCompletion GetCompletion(int level)
    {
        GetLevelsData().Completion.TryGetValue(level, out var value);
        return value;
    }

    private static LevelsData GetLevelsData()
    {
        if (_levelsData == null)
        {
            using var file = new Godot.File();
            var error = file.Open(LevelsData.FilePath, Godot.File.ModeFlags.Read);
            if (error == Godot.Error.Ok)
            {
                string filePath = file.GetPathAbsolute();
                string content = file.GetAsText();
                file.Close();
                try
                {
                    _levelsData = Newtonsoft.Json.JsonConvert.DeserializeObject<LevelsData>(content);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Could not deserialize levels data from {filePath}");
                    Console.Error.WriteLine(ex.ToString());
                }
            }

            _levelsData = _levelsData ?? new LevelsData();
        }

        return _levelsData;
    }

    private static void SaveLevelsData(LevelsData data)
    {
        _levelsData = data;
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        using var file = new Godot.File();
        var error = file.Open(LevelsData.FilePath, Godot.File.ModeFlags.Write);
        if (error != Godot.Error.Ok)
        {
            Console.Error.WriteLine($"Could not save to {file.GetPathAbsolute()}, error {error}");
            return;
        }
        file.StoreString(json);
        file.Close();
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
}
