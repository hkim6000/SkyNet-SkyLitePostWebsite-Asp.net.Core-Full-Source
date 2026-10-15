using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Search : WebPage
    {
        private sealed class View
        {
            public string Title { get; set; } = string.Empty;
            public string Desc { get; set; } = string.Empty;
            public string Main { get; set; } = string.Empty;
            public string Desk { get; set; } = string.Empty;
            public string Section { get; set; } = string.Empty;
        }

        public override async Task OnInitialized()
        {
            SiteData site = await Store.LoadSite();
            List<Staff> staff = await Store.LoadStaff();
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages();
            View v = Render(site, Ui.StaffMap(staff), pages, QueryValue("q"), string.Empty, true);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--sr-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("sr", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("sr", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("sr", site))
                .Replace("{plhd_drawer}", Ui.Drawer("sr", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("sr", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "sr", "foot"))
                .Replace("{plhd_main}", v.Main);
        }

        private static bool SafeColor(string v)
        {
            return Regex.IsMatch(v ?? string.Empty, "^#[0-9a-fA-F]{6}$");
        }

        public async Task<ApiResponse> Hydrate()
        {
            ApiResponse response = new ApiResponse();
            string sbx = Store.Sandbox(GetDataValue("sbx"));
            if (sbx == string.Empty)
            {
                response.ExecuteScript("SearchJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("sr-main", v.Main);
            response.SetElementContents("sr-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sr-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sr-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("sr-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("sr-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("sr-desks", Ui.DeskNav("sr", site, v.Desk));
            response.SetElementContents("sr-subnav", Ui.SubNav("sr", site, v.Desk, v.Section));
            response.SetElementContents("sr-breaking", Ui.BreakingBar("sr", site));
            response.SetElementContents("sr-drawer", Ui.Drawer("sr", site));
            response.SetElementContents("sr-fdesks", Ui.FootDesks("sr", site));
            response.SetElementContents("sr-foot-soc", Social.Html(site.Settings, "sr", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("SearchJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
            return response;
        }

        private static async Task<string> Writable(ApiResponse response, string requested)
        {
            if (!Store.Demo)
            {
                return string.Empty;
            }
            string sbx = Store.Sandbox(requested);
            if (sbx == string.Empty)
            {
                sbx = await Store.NewSandbox();
                response.ExecuteScript("SearchJs.sandboxed('" + sbx + "');");
            }
            return sbx;
        }

        public async Task<ApiResponse> Subscribe()
        {
            ApiResponse response = new ApiResponse();
            string mod = Regex.IsMatch(GetDataValue("mod"), "^[A-Za-z0-9_-]{1,40}$") ? GetDataValue("mod") : string.Empty;
            string mail = Ui.Clip(GetDataValue("mail"), 100);
            if (mod == string.Empty)
            {
                return response;
            }
            if (!IsEmail(mail))
            {
                response.SetElementContents("sr-e-nl-" + mod, "Please enter a valid email address.");
                return response;
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                if (!site.Subscribers.Any(s => s.Email.Equals(mail, StringComparison.OrdinalIgnoreCase)) && site.Subscribers.Count < 50000)
                {
                    site.Subscribers.Add(new Subscriber { Email = mail, When = DateTime.UtcNow });
                    await Store.SaveSite(site, sbx);
                }
            }
            response.ExecuteScript("SearchJs.subscribed('" + mod + "');");
            return response;
        }

        private static bool IsEmail(string v)
        {
            if (v.Length < 5 || v.Length > 80 || v.Contains(' '))
            {
                return false;
            }
            int at = v.IndexOf('@');
            int dot = v.LastIndexOf('.');
            return at > 0 && at == v.LastIndexOf('@') && dot > at + 1 && dot < v.Length - 1;
        }

        private static string NotFound(string p, SiteData site, Dictionary<string, Staff> staff, string what)
        {
            PageModule m = new PageModule { Id = "nf", Style = "rundown", Title = "Latest news", Source = "all", Count = 4 };
            return "<div class=\"" + p + "-wrap\"><div class=\"" + p + "-404\"><h1>We couldn&rsquo;t find that " + what + "</h1><p>It may have moved, or it is not published yet.</p><a class=\"" + p + "-btn\" href=\"Home\">Go to the front page</a></div>" + Ui.Module(p, site, staff, m, new HashSet<string>()) + "</div>";
        }

        private static string Row(string p, SiteData site, Dictionary<string, Staff> staff, Story s, bool kicker = true)
        {
            return "<li>" + (Ui.HasPic(s) ? Ui.Img(p, s, "tpic", true) : string.Empty) + "<div>" + (kicker ? Ui.Kicker(p, site, s) : string.Empty) + Ui.Head(p, s, "h3") + "<p class=\"" + p + "-dek\">" + Ui.Enc(s.Dek) + "</p>" + Ui.Meta(p, s, staff) + "</div></li>";
        }

        private View Render(SiteData site, Dictionary<string, Staff> staff, Dictionary<string, List<PageModule>> pages, string q, string b, bool first)
        {
            string term = Regex.Replace(q ?? string.Empty, "\\s+", " ").Trim();
            term = term.Length > 80 ? term.Substring(0, 80) : term;
            StringBuilder sb = new StringBuilder("<div class=\"sr-wrap\"><div class=\"sr-phead\"><h1>Search</h1></div><form class=\"sr-sform\" action=\"Search\" method=\"get\" role=\"search\"><label class=\"sr-sr\" for=\"sr-term\">Search the Post</label><input type=\"search\" id=\"sr-term\" name=\"q\" value=\"" + Ui.Attr(term) + "\" placeholder=\"Search stories, places, people\" maxlength=\"80\"><button type=\"submit\" class=\"sr-btn\">Search</button></form>");
            if (term == string.Empty)
            {
                sb.Append("<div class=\"sr-chips\">");
                foreach (string t in Ui.LiveStories(site).SelectMany(x => x.Tags).GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Take(14).Select(g => g.Key))
                {
                    sb.Append("<a class=\"sr-chip\" href=\"Search?q=" + Uri.EscapeDataString(t) + "\">" + Ui.Enc(t) + "</a>");
                }
                sb.Append("</div>" + Ui.Module("sr", site, staff, new PageModule { Id = "latest", Style = "rundown", Title = "Latest", Source = "all", Count = 8 }, new HashSet<string>()) + "</div>");
                return new View { Title = "Search", Main = sb.ToString() };
            }
            string[] words = term.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<(Story s, int score)> hits = new List<(Story, int)>();
            foreach (Story s in Ui.LiveStories(site))
            {
                string title = s.Title.ToLowerInvariant();
                string text = (s.Dek + " " + Regex.Replace(s.Body, "<[^>]+>", " ")).ToLowerInvariant();
                string tags = string.Join(" ", s.Tags).ToLowerInvariant();
                string sec = Ui.SectionName(site, s.Section).ToLowerInvariant();
                int score = 0;
                bool all = true;
                foreach (string w in words)
                {
                    int one = (title.Contains(w) ? 6 : 0) + (tags.Contains(w) ? 4 : 0) + (sec.Contains(w) ? 3 : 0) + (text.Contains(w) ? 1 : 0);
                    if (one == 0)
                    {
                        all = false;
                        break;
                    }
                    score += one;
                }
                if (all)
                {
                    hits.Add((s, score));
                }
            }
            List<Story> found = hits.OrderByDescending(h => h.score).ThenByDescending(h => h.s.Date).Select(h => h.s).Take(40).ToList();
            sb.Append("<p class=\"sr-count\">" + found.Count + (found.Count == 1 ? " result" : " results") + " for &ldquo;" + Ui.Enc(term) + "&rdquo;</p>");
            if (found.Count == 0)
            {
                sb.Append("<p class=\"sr-empty\">No stories match. Try fewer or different words.</p>");
            }
            else
            {
                sb.Append("<ul class=\"sr-rows\">");
                foreach (Story s in found)
                {
                    sb.Append(Row("sr", site, staff, s));
                }
                sb.Append("</ul>");
            }
            sb.Append("</div>");
            return new View { Title = "Search: " + term, Main = sb.ToString() };
        }
    }
}
