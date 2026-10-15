using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SkyLitePost.Models;

namespace SkyLitePost.Core
{
    public static class Store
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim LogGate = new SemaphoreSlim(1, 1);
        private static readonly ConcurrentDictionary<string, int> Hits = new ConcurrentDictionary<string, int>();
        private static readonly object HitLock = new object();
        private static bool hitsLoaded;
        private static DateTime hitsSaved = DateTime.UtcNow;

        public static readonly string[] Files = { "site", "pages", "staff", "activity" };

        public static string Root => Directory.GetCurrentDirectory();

        public static string DataDir => Path.Combine(Root, "data");

        public static string Cfg(string key)
        {
            string file = Path.Combine(Root, "appConfig", "application.cfg");
            if (!File.Exists(file))
            {
                return string.Empty;
            }
            foreach (string line in File.ReadAllLines(file))
            {
                int i = line.IndexOf('|');
                if (i > 0 && line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return line.Substring(i + 1).Trim();
                }
            }
            return string.Empty;
        }

        public static bool Demo => Cfg("app.app.runmode") != "1";

        public static string CleanId(string? value)
        {
            string v = (value ?? string.Empty).Trim().ToLowerInvariant();
            return Regex.IsMatch(v, "^[a-f0-9]{32}$") ? v : string.Empty;
        }

        public static string SandboxDir(string sbx)
        {
            return Path.Combine(Root, "temp", "sandbox", sbx);
        }

        public static string Sandbox(string? value)
        {
            string id = CleanId(value);
            return id != string.Empty && Demo && File.Exists(Path.Combine(SandboxDir(id), "site.json")) && Made(id) > DateTime.UtcNow.AddMinutes(-60) ? id : string.Empty;
        }

        public static DateTime Made(string sbx)
        {
            string file = Path.Combine(SandboxDir(sbx), "made");
            return File.Exists(file) && long.TryParse(File.ReadAllText(file), NumberStyles.Integer, CultureInfo.InvariantCulture, out long t) ? new DateTime(t, DateTimeKind.Utc) : Directory.GetCreationTimeUtc(SandboxDir(sbx));
        }

        public static int MinutesLeft(string sbx)
        {
            return Math.Max(1, (int)Math.Ceiling((Made(sbx).AddMinutes(60) - DateTime.UtcNow).TotalMinutes));
        }

        private static string FileOf(string name, string sbx)
        {
            return Path.Combine(sbx == string.Empty ? DataDir : SandboxDir(sbx), name + ".json");
        }

        public static async Task<IDisposable> Hold()
        {
            await Gate.WaitAsync();
            return new Release(Gate);
        }

        private sealed class Release : IDisposable
        {
            private SemaphoreSlim? gate;

            public Release(SemaphoreSlim g)
            {
                gate = g;
            }

            public void Dispose()
            {
                gate?.Release();
                gate = null;
            }
        }

        private static async Task<T> Read<T>(string name, string sbx) where T : new()
        {
            string file = FileOf(name, sbx);
            if (!File.Exists(file))
            {
                file = FileOf(name, string.Empty);
            }
            if (!File.Exists(file))
            {
                return new T();
            }
            try
            {
                return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file), ReadOpts) ?? new T();
            }
            catch (JsonException)
            {
                return new T();
            }
        }

        private static async Task Write<T>(string name, string sbx, T data, bool keepPrevious)
        {
            string file = FileOf(name, sbx);
            Directory.CreateDirectory(Path.GetDirectoryName(file) ?? Root);
            if (keepPrevious && File.Exists(file))
            {
                File.Copy(file, Path.Combine(Path.GetDirectoryName(file) ?? Root, name + ".prev.json"), true);
            }
            string tmp = file + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(data, WriteOpts));
            File.Move(tmp, file, true);
        }

        public static async Task<SiteData> LoadSite(string sbx = "")
        {
            SiteData site = await Read<SiteData>("site", sbx);
            if (Demo && sbx == string.Empty)
            {
                Shift(site, DateTime.UtcNow - site.Anchor);
            }
            return site;
        }

        private static void Shift(SiteData site, TimeSpan d)
        {
            if (site.Anchor == default)
            {
                return;
            }
            d = TimeSpan.FromMinutes(Math.Floor(d.TotalMinutes));
            foreach (Story s in site.Stories)
            {
                s.Date = s.Date.Add(d);
                s.Updated = s.Updated.Add(d);
            }
            foreach (ReaderComment c in site.Comments)
            {
                c.When = c.When.Add(d);
            }
            site.Anchor = site.Anchor.Add(d);
        }

        public static Task SaveSite(SiteData site, string sbx)
        {
            return Write("site", sbx, site, false);
        }

        public static Task<Dictionary<string, List<PageModule>>> LoadPages(string sbx = "")
        {
            return Read<Dictionary<string, List<PageModule>>>("pages", sbx);
        }

        public static Task SavePages(Dictionary<string, List<PageModule>> pages, string sbx)
        {
            return Write("pages", sbx, pages, true);
        }

        public static async Task<Dictionary<string, List<PageModule>>?> PreviousPages(string sbx)
        {
            string file = Path.Combine(sbx == string.Empty ? DataDir : SandboxDir(sbx), "pages.prev.json");
            if (!File.Exists(file))
            {
                return null;
            }
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, List<PageModule>>>(await File.ReadAllTextAsync(file), ReadOpts);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public static Task<List<Staff>> LoadStaff(string sbx = "")
        {
            return Read<List<Staff>>("staff", sbx);
        }

        public static Task SaveStaff(List<Staff> staff, string sbx)
        {
            return Write("staff", sbx, staff, false);
        }

        public static Task<List<Activity>> LoadActivity(string sbx = "")
        {
            return Read<List<Activity>>("activity", sbx);
        }

        public static async Task Log(string sbx, string user, string action, string target, bool ok)
        {
            await LogGate.WaitAsync();
            try
            {
                List<Activity> list = await Read<List<Activity>>("activity", sbx);
                list.Insert(0, new Activity { When = DateTime.UtcNow, User = user, Action = action, Target = target.Length > 120 ? target.Substring(0, 120) : target, Ok = ok });
                if (list.Count > 5000)
                {
                    list.RemoveRange(5000, list.Count - 5000);
                }
                await Write("activity", sbx, list, false);
            }
            finally
            {
                LogGate.Release();
            }
        }

        public static async Task<string> NewSandbox()
        {
            Sweep();
            string id = Guid.NewGuid().ToString("N");
            string dir = SandboxDir(id);
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(Path.Combine(dir, "made"), DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            SiteData site = await Read<SiteData>("site", string.Empty);
            TimeSpan d = TimeSpan.FromMinutes(Math.Floor((DateTime.UtcNow - site.Anchor).TotalMinutes));
            Shift(site, DateTime.UtcNow - site.Anchor);
            await Write("site", id, site, false);
            List<Activity> acts = await Read<List<Activity>>("activity", string.Empty);
            foreach (Activity a in acts)
            {
                a.When = a.When.Add(d);
            }
            await Write("activity", id, acts, false);
            File.Copy(FileOf("pages", string.Empty), FileOf("pages", id), true);
            File.Copy(FileOf("staff", string.Empty), FileOf("staff", id), true);
            return id;
        }

        public static void Sweep()
        {
            DateTime limit = DateTime.UtcNow.AddMinutes(-60);
            string boxes = Path.Combine(Root, "temp", "sandbox");
            string uploads = Path.Combine(Root, "images", "uploads", "sandbox");
            try
            {
                if (Directory.Exists(boxes))
                {
                    foreach (string dir in Directory.GetDirectories(boxes))
                    {
                        if (Made(Path.GetFileName(dir)) < limit)
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
                if (Directory.Exists(uploads))
                {
                    foreach (string dir in Directory.GetDirectories(uploads))
                    {
                        if (!Directory.Exists(SandboxDir(Path.GetFileName(dir))))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static string HitFile => Path.Combine(DataDir, "views.json");

        private static void LoadHits()
        {
            if (hitsLoaded)
            {
                return;
            }
            lock (HitLock)
            {
                if (hitsLoaded)
                {
                    return;
                }
                try
                {
                    if (File.Exists(HitFile))
                    {
                        Dictionary<string, int>? d = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(HitFile));
                        if (d != null)
                        {
                            foreach (KeyValuePair<string, int> kv in d)
                            {
                                Hits[kv.Key] = kv.Value;
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }
                hitsLoaded = true;
            }
        }

        public static int Views(Story s)
        {
            LoadHits();
            return s.Views + (Hits.TryGetValue(s.Id, out int n) ? n : 0);
        }

        public static void Hit(string id)
        {
            LoadHits();
            Hits.AddOrUpdate(id, 1, (k, v) => v + 1);
            if (DateTime.UtcNow - hitsSaved < TimeSpan.FromSeconds(30))
            {
                return;
            }
            lock (HitLock)
            {
                if (DateTime.UtcNow - hitsSaved < TimeSpan.FromSeconds(30))
                {
                    return;
                }
                hitsSaved = DateTime.UtcNow;
                try
                {
                    Directory.CreateDirectory(DataDir);
                    string tmp = HitFile + ".tmp";
                    File.WriteAllText(tmp, JsonSerializer.Serialize(Hits.Where(x => x.Key.Length <= 80).Take(5000).ToDictionary(x => x.Key, x => x.Value)));
                    File.Move(tmp, HitFile, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
