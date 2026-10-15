using System.Globalization;
using System.Text;
using SkyLitePost.Models;

namespace SkyLitePost.Core
{
    public static class Ui
    {
        public static readonly (string Key, string Name, string Hint)[] Styles =
        {
            ("lead", "Lead", "One big story with three headlines beside it"),
            ("leadwide", "Lead Wide", "A full-width photo with the headline on top"),
            ("spotlight", "Spotlight", "One story, photo on the left"),
            ("duo", "Spotlight Duo", "Two stories side by side"),
            ("split", "Split", "A photo story and a list with thumbnails"),
            ("rundown", "Rundown", "A row of four photo cards"),
            ("briefs", "Briefs", "Short text items in columns"),
            ("strip", "Photo Strip", "Large photos you can scroll sideways"),
            ("opinion", "Opinion Row", "Columns with the writer's initials"),
            ("mostread", "Most Read", "A numbered list of the most read stories"),
            ("ticker", "Ticker", "A moving band of the latest headlines"),
            ("notice", "Notice", "A box with your own title and text"),
            ("newsletter", "Newsletter", "A sign-up band for the newsletter"),
            ("follow", "Follow", "Your social links"),
        };

        public static readonly HashSet<string> TextStyles = new HashSet<string> { "notice", "newsletter", "follow" };

        public static string Enc(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Attr(string? v)
        {
            return System.Net.WebUtility.HtmlEncode(v ?? string.Empty);
        }

        public static string Js(string? v)
        {
            return (v ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "&quot;").Replace("<", "\\x3c").Replace("\n", " ").Replace("\r", string.Empty);
        }

        public static string Clip(string? value, int max)
        {
            string v = (value ?? string.Empty).Trim();
            return v.Length > max ? v.Substring(0, max) : v;
        }

        public static string Iso(DateTime d)
        {
            return d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public static string Ago(DateTime d)
        {
            TimeSpan t = DateTime.UtcNow - d.ToUniversalTime();
            if (t.TotalMinutes < 1)
            {
                return "Just now";
            }
            if (t.TotalMinutes < 60)
            {
                int m = (int)t.TotalMinutes;
                return m + (m == 1 ? " min ago" : " mins ago");
            }
            if (t.TotalHours < 24)
            {
                int h = (int)t.TotalHours;
                return h + (h == 1 ? " hour ago" : " hours ago");
            }
            return d.ToUniversalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        }

        public static string Time(DateTime d, string mode = "ago")
        {
            return "<time datetime=\"" + Iso(d) + "\" data-t=\"" + mode + "\">" + (mode == "ago" ? Ago(d) : d.ToUniversalTime().ToString("d MMMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture)) + "</time>";
        }

        public static string Initials(string name)
        {
            string[] parts = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "?";
            }
            string a = parts[0].Substring(0, 1);
            string b = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;
            return (a + b).ToUpperInvariant();
        }

        public static string Hue(string id)
        {
            int h = 0;
            foreach (char c in id ?? string.Empty)
            {
                h = (h * 31 + c) % 360;
            }
            return h.ToString(CultureInfo.InvariantCulture);
        }

        public static string Avatar(string p, Staff? u, string size = "")
        {
            string name = u?.Name ?? "Staff";
            return "<span class=\"" + p + "-av" + (size != string.Empty ? " " + p + "-av-" + size : string.Empty) + "\" style=\"--h:" + Hue(u?.Id ?? name) + "\" aria-hidden=\"true\">" + Enc(Initials(name)) + "</span>";
        }

        public static DeskSection? Section(SiteData site, string key)
        {
            return site.Desks.SelectMany(d => d.Sections).FirstOrDefault(s => s.Key == key);
        }

        public static Desk? DeskOf(SiteData site, string section)
        {
            return site.Desks.FirstOrDefault(d => d.Sections.Any(s => s.Key == section));
        }

        public static string SectionName(SiteData site, string key)
        {
            return Section(site, key)?.Name ?? key;
        }

        public static string Link(Story s)
        {
            return "Article?a=" + Uri.EscapeDataString(s.Id);
        }

        public static string Thumb(string full)
        {
            return full.StartsWith("images/news/full/", StringComparison.Ordinal) ? "images/news/thumbs/" + full.Substring(17) : full.Replace("/full/", "/thumbs/");
        }

        public static IEnumerable<Story> LiveStories(SiteData site)
        {
            return site.Stories.Where(Access.Live).OrderByDescending(s => s.Date);
        }

        public static Story? Breaking(SiteData site)
        {
            return LiveStories(site).FirstOrDefault(s => s.Breaking && s.Date > DateTime.UtcNow.AddHours(-24));
        }

        private static bool InSource(SiteData site, Story s, string source)
        {
            if (source == "front")
            {
                return s.Front;
            }
            if (source.StartsWith("d:", StringComparison.Ordinal))
            {
                Desk? d = DeskOf(site, s.Section);
                return d != null && d.Key == source.Substring(2);
            }
            if (source.StartsWith("s:", StringComparison.Ordinal))
            {
                return s.Section == source.Substring(2);
            }
            return true;
        }

        public static string SourceName(SiteData site, string source)
        {
            if (source == "front")
            {
                return "Front-page picks";
            }
            if (source.StartsWith("d:", StringComparison.Ordinal))
            {
                return site.Desks.FirstOrDefault(d => d.Key == source.Substring(2))?.Name ?? source;
            }
            if (source.StartsWith("s:", StringComparison.Ordinal))
            {
                return SectionName(site, source.Substring(2));
            }
            return "All sections";
        }

        public static bool ValidSource(SiteData site, string source)
        {
            return source == "all" || source == "front"
                || (source.StartsWith("d:", StringComparison.Ordinal) && site.Desks.Any(d => d.Key == source.Substring(2)))
                || (source.StartsWith("s:", StringComparison.Ordinal) && Section(site, source.Substring(2)) != null);
        }

        public static int DefaultCount(string style)
        {
            return style switch
            {
                "lead" => 4,
                "leadwide" or "spotlight" => 1,
                "duo" => 2,
                "split" => 4,
                "rundown" => 4,
                "briefs" => 6,
                "strip" => 5,
                "opinion" => 3,
                "mostread" => 5,
                "ticker" => 8,
                _ => 0
            };
        }

        public static (int Min, int Max) CountRange(string style)
        {
            return style switch
            {
                "lead" => (1, 5),
                "leadwide" or "spotlight" => (1, 1),
                "duo" => (2, 2),
                "split" => (2, 6),
                "rundown" => (4, 8),
                "briefs" => (2, 12),
                "strip" => (3, 10),
                "opinion" => (2, 6),
                "mostread" => (3, 10),
                "ticker" => (3, 12),
                _ => (0, 0)
            };
        }

        public static List<Story> Pick(SiteData site, PageModule m, HashSet<string> used)
        {
            (int min, int max) = CountRange(m.Style);
            int n = Math.Clamp(m.Count <= 0 ? DefaultCount(m.Style) : m.Count, min, max);
            List<Story> pool = LiveStories(site).Where(s => InSource(site, s, m.Source)).ToList();
            if (m.Style == "mostread")
            {
                pool = pool.Where(s => s.Date > DateTime.UtcNow.AddDays(-14)).OrderByDescending(Store.Views).ToList();
            }
            List<Story> list = new List<Story>();
            Story? pin = m.Pin != string.Empty ? LiveStories(site).FirstOrDefault(s => s.Id == m.Pin) : null;
            if (pin != null)
            {
                list.Add(pin);
            }
            bool fresh = m.Style != "ticker" && m.Style != "mostread";
            foreach (Story s in pool)
            {
                if (list.Count >= n)
                {
                    break;
                }
                if (list.Contains(s) || (fresh && used.Contains(s.Id)))
                {
                    continue;
                }
                list.Add(s);
            }
            if (fresh)
            {
                foreach (Story s in list)
                {
                    used.Add(s.Id);
                }
            }
            return list;
        }

        public static string Kicker(string p, SiteData site, Story s)
        {
            return "<a class=\"" + p + "-kick\" href=\"Section?s=" + Uri.EscapeDataString(s.Section) + "\">" + Enc(SectionName(site, s.Section)) + "</a>";
        }

        public static string Meta(string p, Story s, Dictionary<string, Staff> staff, bool by = true)
        {
            string name = staff.TryGetValue(s.Author, out Staff? u) ? u.Name : string.Empty;
            return "<div class=\"" + p + "-meta\">" + (by && name != string.Empty ? "<a href=\"Author?u=" + Uri.EscapeDataString(s.Author) + "\">" + Enc(name) + "</a><i></i>" : string.Empty) + Time(s.Date) + "</div>";
        }

        public static bool HasPic(Story s)
        {
            return s.Image != string.Empty || (s.Video != string.Empty && s.VideoPoster != string.Empty);
        }

        public static string PicSrc(Story s, bool thumb)
        {
            return s.Image != string.Empty ? (thumb ? Thumb(s.Image) : s.Image) : s.VideoPoster;
        }

        public static string PlayBadge(string p, Story s)
        {
            return s.Image == string.Empty && s.Video != string.Empty ? "<span class=\"" + p + "-play\" aria-hidden=\"true\"></span>" : string.Empty;
        }

        public static string PlayButton(string p, Story s)
        {
            return "<button type=\"button\" class=\"" + p + "-play " + p + "-playb\" data-video=\"" + Attr(s.Video) + "\" data-type=\"" + (s.Video.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4") + "\" aria-label=\"Play video: " + Attr(s.Title) + "\"></button>";
        }

        public static string Img(string p, Story s, string cls, bool thumb = false, bool eager = false, bool inline = false)
        {
            if (!HasPic(s))
            {
                return string.Empty;
            }
            if (inline && s.Image == string.Empty && s.Video != string.Empty)
            {
                return "<div class=\"" + p + "-" + cls + " " + p + "-vbox\"><a href=\"" + Link(s) + "\" tabindex=\"-1\" aria-hidden=\"true\"><img src=\"" + Attr(PicSrc(s, thumb)) + "\" alt=\"\"" + (eager ? string.Empty : " loading=\"lazy\"") + "></a>" + PlayButton(p, s) + "</div>";
            }
            return "<a class=\"" + p + "-" + cls + "\" href=\"" + Link(s) + "\" tabindex=\"-1\" aria-hidden=\"true\"><img src=\"" + Attr(PicSrc(s, thumb)) + "\" alt=\"\"" + (eager ? string.Empty : " loading=\"lazy\"") + ">" + PlayBadge(p, s) + "</a>";
        }

        public static string Head(string p, Story s, string tag)
        {
            return "<" + tag + " class=\"" + p + "-hl\"><a href=\"" + Link(s) + "\">" + Enc(s.Title) + "</a></" + tag + ">";
        }

        private static string MoreLink(string p, string source)
        {
            if (source.StartsWith("d:", StringComparison.Ordinal))
            {
                return "<a class=\"" + p + "-more\" href=\"Section?d=" + Uri.EscapeDataString(source.Substring(2)) + "\">More</a>";
            }
            if (source.StartsWith("s:", StringComparison.Ordinal))
            {
                return "<a class=\"" + p + "-more\" href=\"Section?s=" + Uri.EscapeDataString(source.Substring(2)) + "\">More</a>";
            }
            return string.Empty;
        }

        private static string Wrap(string p, PageModule m, string inner, bool head = true)
        {
            string h = head && m.Title != string.Empty ? "<div class=\"" + p + "-mh\"><h2>" + Enc(m.Title) + "</h2>" + MoreLink(p, m.Source) + "</div>" : string.Empty;
            return "<section class=\"" + p + "-mod " + p + "-m-" + m.Style + "\" data-mod=\"" + Attr(m.Id) + "\">" + h + inner + "</section>";
        }

        public static string Module(string p, SiteData site, Dictionary<string, Staff> staff, PageModule m, HashSet<string> used, bool preview = false)
        {
            StringBuilder sb = new StringBuilder();
            if (m.Style == "notice")
            {
                StringBuilder paras = new StringBuilder();
                foreach (string para in m.Text.Replace("\r", string.Empty).Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
                {
                    paras.Append("<p>" + Enc(para.Trim()).Replace("\n", "<br>") + "</p>");
                }
                return Wrap(p, m, "<div class=\"" + p + "-notice\">" + (m.Title != string.Empty ? "<h3>" + Enc(m.Title) + "</h3>" : string.Empty) + paras + "</div>", false);
            }
            if (m.Style == "newsletter")
            {
                string id = p + "-nl-" + m.Id;
                return Wrap(p, m, "<div class=\"" + p + "-nl\"><div><h3>" + Enc(m.Title) + "</h3><p>" + Enc(m.Text) + "</p></div><form class=\"" + p + "-nlf\" data-nl=\"" + Attr(m.Id) + "\" novalidate><label class=\"" + p + "-sr\" for=\"" + id + "\">Email address</label><input type=\"email\" id=\"" + id + "\" placeholder=\"Your email address\" maxlength=\"80\" autocomplete=\"email\"><button type=\"submit\" class=\"" + p + "-btn\">Sign up</button><em class=\"" + p + "-err\" id=\"" + p + "-e-nl-" + Attr(m.Id) + "\"></em></form></div>", false);
            }
            if (m.Style == "follow")
            {
                string links = Social.Html(site.Settings, p, "list");
                if (links == string.Empty)
                {
                    return preview ? Wrap(p, m, "<p class=\"" + p + "-empty\">No social links are switched on in Settings.</p>") : string.Empty;
                }
                return Wrap(p, m, "<div class=\"" + p + "-follow\"><h3>" + Enc(m.Title) + "</h3>" + (m.Text != string.Empty ? "<p>" + Enc(m.Text) + "</p>" : string.Empty) + "<div class=\"" + p + "-socl\">" + links + "</div></div>", false);
            }
            List<Story> list = Pick(site, m, used);
            if (list.Count == 0)
            {
                return preview ? Wrap(p, m, "<p class=\"" + p + "-empty\">No published stories in " + Enc(SourceName(site, m.Source)) + " yet.</p>") : string.Empty;
            }
            switch (m.Style)
            {
                case "lead":
                    {
                        Story a = list[0];
                        sb.Append("<div class=\"" + p + "-lead" + (!HasPic(a) ? " " + p + "-noimg" : string.Empty) + (list.Count == 1 ? " " + p + "-solo" : string.Empty) + "\"><article class=\"" + p + "-lead-a\">" + Img(p, a, "pic", false, true, true) + "<div>" + Kicker(p, site, a) + Head(p, a, "h2") + "<p class=\"" + p + "-dek\">" + Enc(a.Dek) + "</p>" + Meta(p, a, staff) + "</div></article>");
                        if (list.Count > 1)
                        {
                            sb.Append("<div class=\"" + p + "-side\">");
                            for (int i = 1; i < list.Count; i++)
                            {
                                Story s = list[i];
                                sb.Append("<article>" + Kicker(p, site, s) + Head(p, s, "h3") + (i == 1 ? "<p class=\"" + p + "-dek\">" + Enc(s.Dek) + "</p>" : string.Empty) + Meta(p, s, staff, false) + "</article>");
                            }
                            sb.Append("</div>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "leadwide":
                    {
                        Story a = list[0];
                        if (!HasPic(a))
                        {
                            sb.Append("<article class=\"" + p + "-wide " + p + "-noimg\"><div class=\"" + p + "-wide-in\">" + Kicker(p, site, a) + Head(p, a, "h2") + "<p class=\"" + p + "-dek\">" + Enc(a.Dek) + "</p>" + Meta(p, a, staff) + "</div></article>");
                        }
                        else
                        {
                            sb.Append("<article class=\"" + p + "-wide\"><img src=\"" + Attr(PicSrc(a, false)) + "\" alt=\"\">" + (a.Image == string.Empty && a.Video != string.Empty ? PlayButton(p, a) : string.Empty) + "<div class=\"" + p + "-wide-in\">" + Kicker(p, site, a) + Head(p, a, "h2") + "<p class=\"" + p + "-dek\">" + Enc(a.Dek) + "</p>" + Meta(p, a, staff) + "</div></article>");
                        }
                        break;
                    }
                case "spotlight":
                    {
                        Story a = list[0];
                        sb.Append("<article class=\"" + p + "-spot" + (!HasPic(a) ? " " + p + "-noimg" : string.Empty) + "\">" + Img(p, a, "pic", false, false, true) + "<div>" + Kicker(p, site, a) + Head(p, a, "h2") + "<p class=\"" + p + "-dek\">" + Enc(a.Dek) + "</p>" + Meta(p, a, staff) + "</div></article>");
                        break;
                    }
                case "duo":
                    {
                        sb.Append("<div class=\"" + p + "-duo\">");
                        foreach (Story s in list)
                        {
                            sb.Append("<article class=\"" + p + "-card" + (!HasPic(s) ? " " + p + "-noimg" : string.Empty) + "\">" + Img(p, s, "pic", false, false, true) + Kicker(p, site, s) + Head(p, s, "h3") + "<p class=\"" + p + "-dek\">" + Enc(s.Dek) + "</p>" + Meta(p, s, staff) + "</article>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "split":
                    {
                        Story a = list[0];
                        sb.Append("<div class=\"" + p + "-split\"><article class=\"" + p + "-card" + (!HasPic(a) ? " " + p + "-noimg" : string.Empty) + "\">" + Img(p, a, "pic", false, false, true) + Kicker(p, site, a) + Head(p, a, "h3") + "<p class=\"" + p + "-dek\">" + Enc(a.Dek) + "</p>" + Meta(p, a, staff) + "</article><ul class=\"" + p + "-tl\">");
                        for (int i = 1; i < list.Count; i++)
                        {
                            Story s = list[i];
                            sb.Append("<li>" + (HasPic(s) ? Img(p, s, "tpic", true) : string.Empty) + "<div>" + Kicker(p, site, s) + Head(p, s, "h4") + Meta(p, s, staff, false) + "</div></li>");
                        }
                        sb.Append("</ul></div>");
                        break;
                    }
                case "rundown":
                    {
                        sb.Append("<div class=\"" + p + "-run\">");
                        foreach (Story s in list)
                        {
                            sb.Append("<article class=\"" + p + "-card" + (!HasPic(s) ? " " + p + "-noimg" : string.Empty) + "\">" + Img(p, s, "pic", true, false, true) + Kicker(p, site, s) + Head(p, s, "h3") + (!HasPic(s) ? "<p class=\"" + p + "-dek\">" + Enc(s.Dek) + "</p>" : string.Empty) + Meta(p, s, staff, false) + "</article>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "briefs":
                    {
                        sb.Append("<div class=\"" + p + "-briefs\">");
                        foreach (Story s in list)
                        {
                            sb.Append("<article>" + Kicker(p, site, s) + Head(p, s, "h3") + "<p class=\"" + p + "-dek\">" + Enc(s.Dek) + "</p>" + Meta(p, s, staff, false) + "</article>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "strip":
                    {
                        sb.Append("<div class=\"" + p + "-strip\">");
                        foreach (Story s in list)
                        {
                            sb.Append("<a class=\"" + p + "-sp" + (!HasPic(s) ? " " + p + "-noimg" : string.Empty) + "\" href=\"" + Link(s) + "\">" + (HasPic(s) ? "<img src=\"" + Attr(PicSrc(s, true)) + "\" alt=\"\" loading=\"lazy\">" + PlayBadge(p, s) : string.Empty) + "<span><small>" + Enc(SectionName(site, s.Section)) + "</small><b>" + Enc(s.Title) + "</b></span></a>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "opinion":
                    {
                        sb.Append("<div class=\"" + p + "-op\">");
                        foreach (Story s in list)
                        {
                            staff.TryGetValue(s.Author, out Staff? u);
                            sb.Append("<article><div class=\"" + p + "-op-by\">" + Avatar(p, u) + "<div><a href=\"Author?u=" + Uri.EscapeDataString(s.Author) + "\">" + Enc(u?.Name ?? string.Empty) + "</a><small>" + Enc(SectionName(site, s.Section)) + "</small></div></div>" + Head(p, s, "h3") + "<p class=\"" + p + "-dek\">" + Enc(s.Dek) + "</p></article>");
                        }
                        sb.Append("</div>");
                        break;
                    }
                case "mostread":
                    {
                        sb.Append("<ol class=\"" + p + "-mr\">");
                        foreach (Story s in list)
                        {
                            sb.Append("<li><div>" + Kicker(p, site, s) + Head(p, s, "h3") + "</div></li>");
                        }
                        sb.Append("</ol>");
                        break;
                    }
                case "ticker":
                    {
                        StringBuilder items = new StringBuilder();
                        foreach (Story s in list)
                        {
                            items.Append("<a href=\"" + Link(s) + "\"><time datetime=\"" + Iso(s.Date) + "\" data-t=\"hm\">" + s.Date.ToUniversalTime().ToString("HH:mm", CultureInfo.InvariantCulture) + "</time>" + Enc(s.Title) + "</a>");
                        }
                        sb.Append("<div class=\"" + p + "-tick\"><b>" + Enc(m.Title != string.Empty ? m.Title : "Latest") + "</b><div class=\"" + p + "-tick-v\"><div class=\"" + p + "-tick-r\" style=\"--n:" + list.Count + "\">" + items + "<span aria-hidden=\"true\">" + items.ToString().Replace("<a href", "<a tabindex=\"-1\" href") + "</span></div></div></div>");
                        return Wrap(p, m, sb.ToString(), false);
                    }
            }
            return Wrap(p, m, sb.ToString(), m.Style != "lead" || m.Title != string.Empty);
        }

        public static string Modules(string p, SiteData site, Dictionary<string, Staff> staff, List<PageModule> mods, bool preview = false)
        {
            HashSet<string> used = new HashSet<string>();
            StringBuilder sb = new StringBuilder();
            foreach (PageModule m in mods)
            {
                sb.Append(Module(p, site, staff, m, used, preview));
            }
            return sb.ToString();
        }

        public static string DeskNav(string p, SiteData site, string active)
        {
            StringBuilder sb = new StringBuilder("<a class=\"" + p + "-dl" + (active == "home" ? " " + p + "-on" : string.Empty) + "\" href=\"Home\">Home</a>");
            foreach (Desk d in site.Desks)
            {
                sb.Append("<a class=\"" + p + "-dl" + (active == d.Key ? " " + p + "-on" : string.Empty) + "\" href=\"Section?d=" + Uri.EscapeDataString(d.Key) + "\">" + Enc(d.Name) + "</a>");
            }
            return sb.ToString();
        }

        public static string SubNav(string p, SiteData site, string desk, string section)
        {
            Desk? d = site.Desks.FirstOrDefault(x => x.Key == desk);
            if (d == null)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder("<nav class=\"" + p + "-subnav\" aria-label=\"" + Attr(d.Name) + "\"><div class=\"" + p + "-wrap\"><a class=\"" + p + "-sdl\" href=\"Section?d=" + Uri.EscapeDataString(d.Key) + "\">" + Enc(d.Name) + "</a>");
            foreach (DeskSection s in d.Sections)
            {
                sb.Append("<a class=\"" + p + "-sl2" + (s.Key == section ? " " + p + "-on" : string.Empty) + "\" href=\"Section?s=" + Uri.EscapeDataString(s.Key) + "\">" + Enc(s.Name) + "</a>");
            }
            sb.Append("</div></nav>");
            return sb.ToString();
        }

        public static string Drawer(string p, SiteData site)
        {
            StringBuilder sb = new StringBuilder("<a class=\"" + p + "-dr-h\" href=\"Home\">Home</a>");
            foreach (Desk d in site.Desks)
            {
                sb.Append("<div class=\"" + p + "-dr-g\"><a class=\"" + p + "-dr-h\" href=\"Section?d=" + Uri.EscapeDataString(d.Key) + "\">" + Enc(d.Name) + "</a><div>");
                foreach (DeskSection s in d.Sections)
                {
                    sb.Append("<a href=\"Section?s=" + Uri.EscapeDataString(s.Key) + "\">" + Enc(s.Name) + "</a>");
                }
                sb.Append("</div></div>");
            }
            sb.Append("<div class=\"" + p + "-dr-x\"><a href=\"Search\">Search</a><a href=\"About\">About</a><a href=\"AdminLogin\">Newsroom sign in</a></div>");
            return sb.ToString();
        }

        public static string FootDesks(string p, SiteData site)
        {
            StringBuilder sb = new StringBuilder();
            foreach (Desk d in site.Desks)
            {
                sb.Append("<div><a class=\"" + p + "-fd\" href=\"Section?d=" + Uri.EscapeDataString(d.Key) + "\">" + Enc(d.Name) + "</a>");
                foreach (DeskSection s in d.Sections)
                {
                    sb.Append("<a href=\"Section?s=" + Uri.EscapeDataString(s.Key) + "\">" + Enc(s.Name) + "</a>");
                }
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        public static string BreakingBar(string p, SiteData site)
        {
            Story? b = Breaking(site);
            return b == null ? string.Empty : "<div class=\"" + p + "-wrap\"><a href=\"" + Link(b) + "\"><b>Breaking</b><span>" + Enc(b.Title) + "</span></a></div>";
        }

        public static string Pager(string p, int page, int size, int total, string one, string many, string js)
        {
            if (total == 0)
            {
                return string.Empty;
            }
            int pages = Math.Max(1, (total + size - 1) / size);
            string count = total + " " + (total == 1 ? one : many);
            return "<div class=\"" + p + "-pager\"><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page - 1) + ")\"" + (page <= 1 ? " disabled" : string.Empty) + ">&larr; Previous</button><span>Page " + page + " of " + pages + " &middot; " + count + "</span><button type=\"button\" class=\"" + p + "-btn " + p + "-btn-sm " + p + "-btn-ghost\" onclick=\"" + js + "(" + (page + 1) + ")\"" + (page >= pages ? " disabled" : string.Empty) + ">Next &rarr;</button></div>";
        }

        public static int PageOf(string value, int size, int total)
        {
            int pages = Math.Max(1, (total + size - 1) / size);
            return Math.Clamp(int.TryParse(value, out int n) ? n : 1, 1, pages);
        }

        public static Dictionary<string, Staff> StaffMap(List<Staff> list)
        {
            Dictionary<string, Staff> d = new Dictionary<string, Staff>();
            foreach (Staff s in list)
            {
                d[s.Id] = s;
            }
            return d;
        }
    }
}
