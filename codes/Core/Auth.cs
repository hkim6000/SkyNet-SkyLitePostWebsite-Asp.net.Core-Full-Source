using System.Globalization;
using System.Text;
using System.Text.Json;
using SkyLitePost.Models;

namespace SkyLitePost.Core
{
    public sealed class Ctx
    {
        public Session S { get; set; } = new Session();
        public Staff U { get; set; } = new Staff();
        public string Sbx => S.Sbx;
    }

    public static class Auth
    {
        private static readonly JsonSerializerOptions ReadOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
        private static readonly SemaphoreSlim SessGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, List<DateTime>> Fails = new Dictionary<string, List<DateTime>>();

        private static string SessionFile => Path.Combine(Store.Root, "temp", "admin", "sessions.json");

        private static async Task<List<Session>> ReadSessions()
        {
            if (!File.Exists(SessionFile))
            {
                return new List<Session>();
            }
            try
            {
                return JsonSerializer.Deserialize<List<Session>>(await File.ReadAllTextAsync(SessionFile), ReadOpts) ?? new List<Session>();
            }
            catch (JsonException)
            {
                return new List<Session>();
            }
        }

        private static async Task WriteSessions(List<Session> list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionFile) ?? Store.Root);
            string tmp = SessionFile + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(list, WriteOpts));
            File.Move(tmp, SessionFile, true);
        }

        public static bool Locked(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    return false;
                }
                list.RemoveAll(f => f < DateTime.UtcNow.AddMinutes(-5));
                return list.Count >= 6;
            }
        }

        public static void Fail(string login)
        {
            lock (Fails)
            {
                string k = login.ToLowerInvariant();
                if (Fails.Count > 2000)
                {
                    Fails.Clear();
                }
                if (!Fails.TryGetValue(k, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Fails[k] = list;
                }
                list.Add(DateTime.UtcNow);
            }
        }

        public static async Task<string> Open(string sbx, string user, DateTime exp)
        {
            string tok = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                list.RemoveAll(x => x.Exp < DateTime.UtcNow);
                list.Add(new Session { Tok = tok, Sbx = sbx, User = user, Exp = exp });
                await WriteSessions(list.OrderByDescending(x => x.Exp).Take(1000).ToList());
            }
            finally
            {
                SessGate.Release();
            }
            return tok;
        }

        public static async Task Close(string tok)
        {
            string t = Store.CleanId(tok);
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Tok == t) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task CloseUser(string sbx, string user, string keep)
        {
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                if (list.RemoveAll(x => x.Sbx == sbx && x.User == user && x.Tok != keep) > 0)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
        }

        public static async Task<Ctx?> Check(string? tokValue)
        {
            string tok = Store.CleanId(tokValue);
            if (tok == string.Empty)
            {
                return null;
            }
            Session? s;
            await SessGate.WaitAsync();
            try
            {
                List<Session> list = await ReadSessions();
                int before = list.Count;
                list.RemoveAll(x => x.Exp < DateTime.UtcNow || (x.Sbx != string.Empty && Store.Sandbox(x.Sbx) == string.Empty) || (x.Sbx == string.Empty && Store.Demo));
                s = list.FirstOrDefault(x => x.Tok == tok);
                bool slide = s != null && s.Sbx == string.Empty && s.Exp < DateTime.UtcNow.AddHours(7);
                if (s != null && slide)
                {
                    s.Exp = DateTime.UtcNow.AddHours(8);
                }
                if (list.Count != before || slide)
                {
                    await WriteSessions(list);
                }
            }
            finally
            {
                SessGate.Release();
            }
            if (s == null)
            {
                return null;
            }
            List<Staff> staff = await Store.LoadStaff(s.Sbx);
            Staff? u = staff.FirstOrDefault(x => x.Id == s.User && x.Active);
            return u == null ? null : new Ctx { S = s, U = u };
        }

        public static string Menu(string p, string page, Ctx c, SiteData site)
        {
            List<(string Key, string Name)> items = new List<(string, string)> { ("Admin", "Dashboard"), ("AdminArticles", "Stories"), ("AdminMedia", "Media") };
            bool pages = c.U.Admin || c.U.FrontPage || site.Desks.Any(d => Access.DeskPublisher(c.U, d));
            if (pages)
            {
                items.Add(("AdminPages", "Pages"));
            }
            if (c.U.Admin)
            {
                items.Add(("AdminDesks", "Departments"));
            }
            if (Access.AnyPublish(c.U, site))
            {
                items.Add(("AdminComments", "Comments"));
            }
            if (c.U.Admin)
            {
                items.Add(("AdminUsers", "Users"));
            }
            items.Add(("AdminActivity", "Activity"));
            if (c.U.Admin)
            {
                items.Add(("AdminSettings", "Settings"));
            }
            items.Add(("AdminAccount", "My account"));
            int review = site.Stories.Count(s => s.Status == "review" && Access.Can(c.U, s.Section, 'P'));
            int pending = site.Comments.Count(x => x.Status == "pending" && site.Stories.Any(s => s.Id == x.Story && Access.Can(c.U, s.Section, 'P')));
            StringBuilder sb = new StringBuilder();
            foreach ((string k, string n) in items)
            {
                int cnt = k == "AdminArticles" ? review : k == "AdminComments" ? pending : 0;
                sb.Append("<li><a class=\"" + p + "-al" + (k == page ? " " + p + "-on" : string.Empty) + "\" href=\"" + k + "\">" + n + (cnt > 0 ? "<i class=\"" + p + "-cnt\">" + cnt.ToString(CultureInfo.InvariantCulture) + "</i>" : string.Empty) + "</a></li>");
            }
            return sb.ToString();
        }

        public static string Who(string p, Ctx c)
        {
            return Ui.Avatar(p, c.U) + "<span class=\"" + p + "-who-t\"><b>" + Ui.Enc(c.U.Name) + "</b><small>" + Ui.Enc(c.U.Title) + "</small></span>";
        }

        public static string DemoPill(string p, Ctx c)
        {
            return c.Sbx != string.Empty ? "<span class=\"" + p + "-dl\">Demo copy &middot; resets in </span><span class=\"" + p + "-ds\">Demo &middot; </span>" + Store.MinutesLeft(c.Sbx) + " min" : string.Empty;
        }

        public static string StatusPill(string p, Story s)
        {
            string st = s.Status == "scheduled" && s.Date <= DateTime.UtcNow ? "published" : s.Status;
            string label = st switch { "published" => "Published", "scheduled" => "Scheduled", "review" => "In review", "suspended" => "Suspended", _ => "Draft" };
            return "<span class=\"" + p + "-st " + p + "-st-" + st + "\">" + label + "</span>";
        }
    }
}
