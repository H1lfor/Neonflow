using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Neonflow
{
    [Serializable]
    public class Track
    {
        public int id;
        public string artist, title, fileName;
        public double bpm, firstBeatOffsetSeconds;
        public bool timingVerifiedByListening;
        public double Window => 480d / bpm;

        public double Target(int i) => firstBeatOffsetSeconds + i * Window;
    }

    [Serializable]
    public class Tracks
    {
        public Track[] tracks;
    }

    [Serializable]
    public class Word
    {
        public string id, text;
    }

    [Serializable]
    public class Words
    {
        public Word[] strings;
    }

    [Serializable]
    public class Movement
    {
        public string id, label, category, path;
        public string[] fingerCueAllowedHands;
    }

    [Serializable]
    public class MovementCatalogData
    {
        public Movement[] poses;
    }

    [Serializable]
    public class Balance
    {
        public double grace = .18, sampleSpan = .12, inferenceWait = .25;
        public float holdThreshold = .65f, perfect = .85f, good = .25f, hp = 100, damageBudget = 210, robotError = .23f;
        public float healingMin = .5f, healingMax = 10, healingExponent = 1.5f;
        public float fullAccuracy = .64f;
    }

    [Serializable]
    public class Cue
    {
        public string pose, left, right;
        public double start, target;
        public float robotAccuracy;
    }

    [Serializable]
    public class Settings
    {
        public float music = .7f, ui = .65f, effects = .5f;
        public bool voice = true;
        public bool reducedMotion;
        public string camera = "";
        public int microphone = -1;
        public string characterId = "light";
    }

    [Serializable]
    public class RunRecord
    {
        public int trackId, seed, score, maxCombo, windows;
        public float accuracy;
        public bool practice, completed, victory;
        public string date, rank;
        public string characterId;
        public int[] judgements;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public Settings settings = new Settings();
        public List<RunRecord> runs = new List<RunRecord>();
    }

    public static class SaveStore
    {
        public static string OverridePath;
        public static string PathName => OverridePath ?? Path.Combine(Application.persistentDataPath, "neonflow-v1.json");

        public static SaveData Load()
        {
            foreach (var path in new[]
            {
                PathName,
                PathName + ".bak"
            }

            )
                try
                {
                    if (File.Exists(path))
                    {
                        var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                        if (d != null && d.settings != null && d.runs != null)
                            return d;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.Message);
                }

            return new SaveData();
        }

        public static void Write(SaveData data, string path = null)
        {
            path = path ?? PathName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(path))
                File.Replace(temp, path, path + ".bak");
            else
                File.Move(temp, path);
        }
    }

    public static class Generator
    {
        public static List<Cue> Create(Track track, double length, Movement[] movements, int seed, Balance b)
        {
            var rng = new System.Random(seed);
            var poses = Array.FindAll(movements, m => m.category == "Poses");
            var hands = Array.FindAll(movements, m => m.category == "Hands");
            var result = new List<Cue>();
            string last = "";
            int errors = 0, clean = 0;
            for (int i = 0; track.Target(i) <= length; i++)
            {
                Movement p;
                do
                {
                    p = poses[rng.Next(poses.Length)];
                }
                while (p.id == last);
                last = p.id;
                var c = new Cue
                {
                    pose = p.id,
                    start = track.Target(i) - track.Window,
                    target = track.Target(i)
                };
                // Clasped/crossed hands cannot reliably display independent fingers.
                if (p.id != "clap" && p.id != "cross_chest")
                    foreach (var side in p.fingerCueAllowedHands ?? Array.Empty<string>())
                    {
                        var h = hands[rng.Next(hands.Length)].id;
                        if (side == "left")
                            c.left = h;
                        else
                            c.right = h;
                    }

                bool fail = errors < 2 && (clean >= 7 || rng.NextDouble() < b.robotError);
                c.robotAccuracy = fail ? (float)(.15 + rng.NextDouble() * .32) : (float)(.86 + rng.NextDouble() * .14);
                errors = fail ? errors + 1 : 0;
                clean = fail ? 0 : clean + 1;
                result.Add(c);
            }

            return result;
        }
    }

    public struct Observation
    {
        public double time;
        public float accuracy;
        public Observation(double t, float a)
        {
            time = t;
            accuracy = a;
        }
    }

    public sealed class WindowScore
    {
        readonly List<Observation> samples = new List<Observation>();
        readonly Cue cue;
        readonly Balance b;
        public bool Scored { get; private set; }

        public WindowScore(Cue c, Balance balance)
        {
            cue = c;
            b = balance;
        }

        public void Add(double time, float accuracy)
        {
            if (Scored || time < cue.start || time > cue.target || double.IsNaN(time))
                return;
            if (samples.Count > 0 && time <= samples[samples.Count - 1].time)
                return;
            samples.Add(new Observation(time, Mathf.Clamp01(accuracy)));
        }

        public (float accuracy, float hold) Finish()
        {
            if (Scored)
                throw new InvalidOperationException("Window already scored");
            Scored = true;
            double hold = 0;
            float final = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                double end = i + 1 < samples.Count ? samples[i + 1].time : cue.target;
                if (s.accuracy >= b.holdThreshold)
                    hold += Math.Max(0, Math.Min(end - s.time, b.sampleSpan));
                if (cue.target - s.time <= b.grace)
                    final = Math.Max(final, s.accuracy);
            }

            return (final, (float)Math.Min(1, hold / (cue.target - cue.start)));
        }
    }

    public sealed class Battle
    {
        public readonly List<Cue> cues;
        public readonly WindowScore[] windows;
        public readonly RunRecord record;
        readonly Balance b;
        public float playerHP, robotHP;
        public int combo, index;
        float sum;
        public string LastJudgement = "";
        public float LastAccuracy;
        public float LastHealing;
        public Battle(Track track, double length, Movement[] moves, int seed, bool practice, Balance balance, string characterId = null)
        {
            b = balance;
            cues = Generator.Create(track, length, moves, seed, b);
            windows = cues.ConvertAll(c => new WindowScore(c, b)).ToArray();
            playerHP = robotHP = b.hp;
            record = new RunRecord
            {
                trackId = track.id,
                characterId = characterId,
                seed = seed,
                practice = practice,
                date = DateTime.UtcNow.ToString("o"),
                judgements = new int[4]
            };
        }

        public void Observe(double time, Func<Cue, float> evaluate)
        {
            foreach (var i in CandidateWindows(time))
                windows[i].Add(time, evaluate(cues[i]));
        }

        IEnumerable<int> CandidateWindows(double time)
        {
            for (int i = Math.Max(0, index); i < cues.Count; i++)
            {
                if (time < cues[i].start)
                    yield break;
                if (time <= cues[i].target)
                    yield return i;
            }
        }

        public void Advance(double time)
        {
            while (!Defeated && index < cues.Count && time >= cues[index].target + b.inferenceWait)
            {
                var s = windows[index].Finish();
                Apply(s.accuracy, s.hold, cues[index].robotAccuracy);
                index++;
            }
        }

        public void Apply(float accuracy, float hold, float robotAccuracy)
        {
            combo = accuracy < .5f ? 0 : combo + 1;
            record.maxCombo = Math.Max(combo, record.maxCombo);
            record.score += Mathf.RoundToInt(1000 * accuracy * (.25f + .75f * hold) * (1 + Math.Min(combo / 10f, 3)));
            var j = accuracy >= b.perfect ? 0 : accuracy >= .5f ? 1 : accuracy >= b.good ? 2 : 3;
            record.judgements[j]++;
            LastJudgement = new[]
            {
                "Perfect",
                "Great",
                "Good",
                "Miss"
            }[j];
            LastAccuracy = accuracy;
            sum += accuracy;
            record.windows++;
            record.accuracy = sum / record.windows;
            float damage = b.damageBudget / Math.Max(1, cues.Count);
            LastHealing = 0;
            if (accuracy >= .5f)
            {
                float healing = Mathf.Lerp(b.healingMin, b.healingMax, Mathf.Pow(Mathf.Clamp01((accuracy - .5f) / .5f), b.healingExponent));
                float before = playerHP;
                playerHP = Mathf.Min(b.hp, playerHP + healing);
                LastHealing = playerHP - before;
            }
            else
                playerHP = Math.Max(0, playerHP - damage * Math.Max(0, 1 - accuracy / b.perfect));
            robotHP = Math.Max(0, robotHP - damage * Math.Max(0, 1 - robotAccuracy / b.perfect));
        }

        public bool Defeated => playerHP <= 0 && !record.practice;

        public void Complete(bool completed)
        {
            record.completed = completed;
            record.victory = completed && (playerHP > 0 || record.practice);
            record.rank = Rank(record.accuracy);
        }

        public static string Rank(float a) => a >= .97 ? "S+" : a >= .9 ? "S" : a >= .8 ? "A" : a >= .7 ? "B" : a >= .6 ? "C" : a >= .5 ? "D" : "E";
    }
}
