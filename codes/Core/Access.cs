using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Models;

namespace SkyLitePost.Core
{
    public static class Access
    {
        public const string Letters = "VUEDP";

        public static string Perm(Staff u, string section)
        {
            if (u.Admin)
            {
                return Letters;
            }
            string p = u.Perms.TryGetValue(section, out string? v) ? v ?? string.Empty : string.Empty;
            if (p.Contains('P'))
            {
                return Letters;
            }
            return p.Length > 0 && !p.Contains('V') ? "V" + p : p;
        }

        public static bool Can(Staff u, string section, char letter)
        {
            return Perm(u, section).Contains(letter);
        }

        public static bool Live(Story s)
        {
            return s.Status == "published" || (s.Status == "scheduled" && s.Date <= DateTime.UtcNow);
        }

        public static bool CanEdit(Staff u, Story s)
        {
            if (Live(s) || s.Status == "scheduled" || s.Status == "suspended")
            {
                return Can(u, s.Section, 'P');
            }
            return Can(u, s.Section, 'E') || (s.Author == u.Id && Can(u, s.Section, 'U'));
        }

        public static bool CanDelete(Staff u, Story s)
        {
            if (Live(s) || s.Status == "scheduled" || s.Status == "suspended")
            {
                return Can(u, s.Section, 'P');
            }
            return Can(u, s.Section, 'D') || (s.Author == u.Id && Can(u, s.Section, 'U'));
        }

        public static bool DeskPublisher(Staff u, Desk d)
        {
            return u.Admin || (d.Sections.Count > 0 && d.Sections.All(s => Can(u, s.Key, 'P')));
        }

        public static bool CanPage(Staff u, string key, SiteData site)
        {
            if (key == "home")
            {
                return u.Admin || u.FrontPage;
            }
            Desk? d = site.Desks.FirstOrDefault(x => x.Key == key);
            return d != null && DeskPublisher(u, d);
        }

        public static bool AnyUpload(Staff u, SiteData site)
        {
            return u.Admin || site.Desks.SelectMany(d => d.Sections).Any(s => Can(u, s.Key, 'U'));
        }

        public static bool AnyPublish(Staff u, SiteData site)
        {
            return u.Admin || site.Desks.SelectMany(d => d.Sections).Any(s => Can(u, s.Key, 'P'));
        }

        public static List<DeskSection> Sections(Staff u, SiteData site, char letter)
        {
            return site.Desks.SelectMany(d => d.Sections).Where(s => Can(u, s.Key, letter)).ToList();
        }

        public static string HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            byte[] dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            return "pbkdf2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(dk);
        }

        public static bool Verify(string password, string stored)
        {
            string[] parts = (stored ?? string.Empty).Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out int iter) || iter < 10000 || iter > 1000000)
            {
                return false;
            }
            try
            {
                byte[] salt = Convert.FromBase64String(parts[2]);
                byte[] want = Convert.FromBase64String(parts[3]);
                byte[] got = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iter, HashAlgorithmName.SHA256, want.Length);
                return CryptographicOperations.FixedTimeEquals(got, want);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static bool VerifySha(string password, string hex)
        {
            string hash = (hex ?? string.Empty).ToLowerInvariant();
            if (!Regex.IsMatch(hash, "^[a-f0-9]{64}$"))
            {
                return false;
            }
            string given = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(hash));
        }

        private static readonly HashSet<string> Allowed = new HashSet<string> { "p", "h2", "h3", "strong", "em", "a", "ul", "ol", "li", "blockquote", "img", "figure", "figcaption", "br" };

        private static string AttrValue(string attrs, string name)
        {
            Match m = Regex.Match(attrs, "\\b" + name + "\\s*=\\s*(\"([^\"]*)\"|'([^']*)'|([^\\s>]+))", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return string.Empty;
            }
            string v = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
            return System.Net.WebUtility.HtmlDecode(v).Trim();
        }

        public static bool SafeHref(string h)
        {
            return Regex.IsMatch(h, "^(https?://[^\\s\"'<>]+|mailto:[^\\s\"'<>]+|[A-Za-z][A-Za-z0-9]*(\\?[A-Za-z0-9=&%._-]*)?)$");
        }

        public static bool SafeImage(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(webp|jpg|jpeg|png)$") && !src!.Contains("..");
        }

        public static bool SafeVideo(string src)
        {
            return Regex.IsMatch(src ?? string.Empty, "^images/[A-Za-z0-9/_.-]+\\.(mp4|webm)$") && !src!.Contains("..");
        }

        private static string Plain(string t)
        {
            return Ui.Enc(System.Net.WebUtility.HtmlDecode(t));
        }

        public static string Sanitize(string html)
        {
            string s = Regex.Replace(html ?? string.Empty, "<(script|style|iframe|object|embed|svg|math|template|textarea|select)\\b[^>]*>[\\s\\S]*?</\\1\\s*>", string.Empty, RegexOptions.IgnoreCase);
            s = Regex.Replace(s, "<!--[\\s\\S]*?-->", string.Empty);
            StringBuilder sb = new StringBuilder();
            List<string> open = new List<string>();
            int pos = 0;
            foreach (Match m in Regex.Matches(s, "<(/?)([a-zA-Z][a-zA-Z0-9]*)\\b([^>]*)>"))
            {
                sb.Append(Plain(s.Substring(pos, m.Index - pos)));
                pos = m.Index + m.Length;
                string name = m.Groups[2].Value.ToLowerInvariant();
                name = name switch { "b" => "strong", "i" => "em", "div" => "p", "h1" => "h2", "h4" or "h5" or "h6" => "h3", _ => name };
                if (!Allowed.Contains(name))
                {
                    continue;
                }
                bool close = m.Groups[1].Value == "/";
                if (name == "br")
                {
                    sb.Append(close ? string.Empty : "<br>");
                    continue;
                }
                if (name == "img")
                {
                    string src = AttrValue(m.Groups[3].Value, "src");
                    if (!close && SafeImage(src))
                    {
                        sb.Append("<img src=\"" + Ui.Attr(src) + "\" alt=\"" + Ui.Attr(AttrValue(m.Groups[3].Value, "alt")) + "\">");
                    }
                    continue;
                }
                if (close)
                {
                    int at = open.LastIndexOf(name);
                    if (at >= 0)
                    {
                        for (int i = open.Count - 1; i >= at; i--)
                        {
                            sb.Append("</" + open[i] + ">");
                        }
                        open.RemoveRange(at, open.Count - at);
                    }
                    continue;
                }
                if ((name == "p" || name == "h2" || name == "h3" || name == "figure") && open.Count > 0 && (open[^1] == "p" || open[^1] == "h2" || open[^1] == "h3"))
                {
                    sb.Append("</" + open[^1] + ">");
                    open.RemoveAt(open.Count - 1);
                }
                if (name == "a")
                {
                    string href = AttrValue(m.Groups[3].Value, "href");
                    if (!SafeHref(href))
                    {
                        continue;
                    }
                    sb.Append("<a href=\"" + Ui.Attr(href) + "\"" + (href.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? " target=\"_blank\" rel=\"noopener\"" : string.Empty) + ">");
                    open.Add("a");
                    continue;
                }
                sb.Append("<" + name + ">");
                open.Add(name);
            }
            sb.Append(Plain(s.Substring(pos)));
            for (int i = open.Count - 1; i >= 0; i--)
            {
                sb.Append("</" + open[i] + ">");
            }
            string r = sb.ToString();
            string before;
            do
            {
                before = r;
                r = Regex.Replace(r, "<(p|h2|h3|strong|em|blockquote|li|ul|ol|figcaption|a)>(\\s|&#160;|<br>)*</\\1>", string.Empty);
            }
            while (r != before);
            return r.Trim();
        }
    }
}
