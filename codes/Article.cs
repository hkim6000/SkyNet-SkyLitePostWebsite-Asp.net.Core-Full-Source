using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SkyLitePost.Core;
using SkyLitePost.Models;
using SkyNet;

namespace SkyLitePost.codes
{
    public class Article : WebPage
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
            View v = Render(site, Ui.StaffMap(staff), pages, QueryValue("a"), string.Empty, true);
            HtmlDoc.SetTitle((v.Title != string.Empty ? v.Title + " | " : string.Empty) + site.Settings.Name);
            HtmlDoc.AddMetaElement("viewport", "width=device-width, initial-scale=1");
            HtmlDoc.AddMetaElement("description", v.Desc != string.Empty ? v.Desc : site.Settings.Tagline);
            HtmlDoc.HtmlBodyText = HtmlDoc.HtmlBodyText
                .Replace("{plhd_name}", Ui.Enc(site.Settings.Name))
                .Replace("{plhd_tagline}", Ui.Enc(site.Settings.Tagline))
                .Replace("{plhd_theme}", site.Settings.Theme == "dark" ? "dark" : "light")
                .Replace("{plhd_style}", SafeColor(site.Settings.Accent) ? "--ar-accent:" + site.Settings.Accent : string.Empty)
                .Replace("{plhd_now}", Ui.Iso(DateTime.UtcNow))
                .Replace("{plhd_today}", DateTime.UtcNow.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture))
                .Replace("{plhd_nav}", Ui.DeskNav("ar", site, v.Desk))
                .Replace("{plhd_subnav}", Ui.SubNav("ar", site, v.Desk, v.Section))
                .Replace("{plhd_breaking}", Ui.BreakingBar("ar", site))
                .Replace("{plhd_drawer}", Ui.Drawer("ar", site))
                .Replace("{plhd_footdesks}", Ui.FootDesks("ar", site))
                .Replace("{plhd_social}", Social.Html(site.Settings, "ar", "foot"))
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
                response.ExecuteScript("ArticleJs.hydrated(false);");
                return response;
            }
            SiteData site = await Store.LoadSite(sbx);
            List<Staff> staff = await Store.LoadStaff(sbx);
            Dictionary<string, List<PageModule>> pages = await Store.LoadPages(sbx);
            View v = Render(site, Ui.StaffMap(staff), pages, Ui.Clip(GetDataValue("argA"), 120), Ui.Clip(GetDataValue("argB"), 120), false);
            response.SetElementContents("ar-main", v.Main);
            response.SetElementContents("ar-brand", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ar-fname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ar-cname", Ui.Enc(site.Settings.Name));
            response.SetElementContents("ar-tagline", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ar-ftag", Ui.Enc(site.Settings.Tagline));
            response.SetElementContents("ar-desks", Ui.DeskNav("ar", site, v.Desk));
            response.SetElementContents("ar-subnav", Ui.SubNav("ar", site, v.Desk, v.Section));
            response.SetElementContents("ar-breaking", Ui.BreakingBar("ar", site));
            response.SetElementContents("ar-drawer", Ui.Drawer("ar", site));
            response.SetElementContents("ar-fdesks", Ui.FootDesks("ar", site));
            response.SetElementContents("ar-foot-soc", Social.Html(site.Settings, "ar", "foot"));
            string accent = SafeColor(site.Settings.Accent) ? site.Settings.Accent : string.Empty;
            response.ExecuteScript("ArticleJs.hydrated(true,'" + accent + "','" + (site.Settings.Theme == "dark" ? "dark" : "light") + "');");
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
                response.ExecuteScript("ArticleJs.sandboxed('" + sbx + "');");
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
                response.SetElementContents("ar-e-nl-" + mod, "Please enter a valid email address.");
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
            response.ExecuteScript("ArticleJs.subscribed('" + mod + "');");
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

        private View Render(SiteData site, Dictionary<string, Staff> staff, Dictionary<string, List<PageModule>> pages, string a, string b, bool first)
        {
            Story? s = site.Stories.FirstOrDefault(x => x.Id == a && Access.Live(x));
            if (s == null)
            {
                return new View { Title = "Not found", Main = NotFound("ar", site, staff, "story") };
            }
            if (first)
            {
                Store.Hit(s.Id);
            }
            Desk? desk = Ui.DeskOf(site, s.Section);
            staff.TryGetValue(s.Author, out Staff? u);
            StringBuilder sb = new StringBuilder("<article class=\"ar-art\"><header class=\"ar-ahead\">" + Ui.Kicker("ar", site, s) + "<h1>" + (s.Breaking && s.Date > DateTime.UtcNow.AddHours(-24) ? "<span class=\"ar-flag\">Breaking</span>" : string.Empty) + Ui.Enc(s.Title) + "</h1>");
            if (s.Dek != string.Empty)
            {
                sb.Append("<p class=\"ar-dek\">" + Ui.Enc(s.Dek) + "</p>");
            }
            sb.Append("<div class=\"ar-by\">" + Ui.Avatar("ar", u, "md") + "<div>" + (u != null ? "<a href=\"Author?u=" + Uri.EscapeDataString(u.Id) + "\">" + Ui.Enc(u.Name) + "</a>" : string.Empty) + "<small>" + Ui.Time(s.Date, "full") + (s.Updated > s.Date.AddMinutes(5) ? " &middot; Updated " + Ui.Time(s.Updated) : string.Empty) + "</small></div></div></header>");
            string video = s.Video != string.Empty ? "<video controls preload=\"none\" playsinline" + (s.VideoPoster != string.Empty ? " poster=\"" + Ui.Attr(s.VideoPoster) + "\"" : string.Empty) + "><source src=\"" + Ui.Attr(s.Video) + "\" type=\"" + (s.Video.EndsWith(".webm", StringComparison.OrdinalIgnoreCase) ? "video/webm" : "video/mp4") + "\"></video>" + (s.VideoCaption != string.Empty ? "<figcaption>" + Ui.Enc(s.VideoCaption) + "</figcaption>" : string.Empty) : string.Empty;
            if (s.Image != string.Empty)
            {
                sb.Append("<figure class=\"ar-afig\"><img src=\"" + Ui.Attr(s.Image) + "\" alt=\"" + Ui.Attr(s.Caption) + "\">" + (s.Caption != string.Empty ? "<figcaption>" + Ui.Enc(s.Caption) + "</figcaption>" : string.Empty) + "</figure>");
            }
            else if (video != string.Empty)
            {
                sb.Append("<figure class=\"ar-afig\">" + video + "</figure>");
            }
            sb.Append("<div class=\"ar-agrid\"><div><div class=\"ar-body\">" + s.Body + "</div>");
            if (s.Image != string.Empty && video != string.Empty)
            {
                sb.Append("<figure class=\"ar-avid\">" + video + "</figure>");
            }
            if (s.Tags.Count > 0)
            {
                sb.Append("<div class=\"ar-tags\">");
                foreach (string t in s.Tags)
                {
                    sb.Append("<a class=\"ar-tag\" href=\"Search?q=" + Uri.EscapeDataString(t) + "\">" + Ui.Enc(t) + "</a>");
                }
                sb.Append("</div>");
            }
            sb.Append(Comments(site, s));
            sb.Append("</div><aside class=\"ar-rail\"><h2>Most read</h2><ol>");
            foreach (Story x in Ui.LiveStories(site).Where(x => x.Id != s.Id && x.Date > DateTime.UtcNow.AddDays(-14)).OrderByDescending(Store.Views).Take(5))
            {
                sb.Append("<li><div>" + Ui.Head("ar", x, "h3") + "</div></li>");
            }
            sb.Append("</ol></aside></div>");
            HashSet<string> used = new HashSet<string> { s.Id };
            sb.Append("<div class=\"ar-more-in\">" + Ui.Module("ar", site, staff, new PageModule { Id = "more", Style = "rundown", Title = "More in " + Ui.SectionName(site, s.Section), Source = desk != null ? "d:" + desk.Key : "all", Count = 4 }, used) + "</div></article>");
            return new View { Title = s.Title, Desc = s.Dek, Desk = desk?.Key ?? string.Empty, Section = s.Section, Main = sb.ToString() };
        }

        private static string CommentList(SiteData site, Story s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (ReaderComment c in site.Comments.Where(c => c.Story == s.Id && c.Status == "approved").OrderBy(c => c.When))
            {
                sb.Append("<div class=\"ar-cmt\"><b>" + Ui.Enc(c.Name) + "</b>" + Ui.Time(c.When) + "<p>" + Ui.Enc(c.Text) + "</p></div>");
            }
            return sb.ToString();
        }

        private static string Comments(SiteData site, Story s)
        {
            if (!site.Settings.Comments || !s.AllowComments)
            {
                return string.Empty;
            }
            int n = site.Comments.Count(c => c.Story == s.Id && c.Status == "approved");
            return "<section class=\"ar-cmts\" id=\"comments\"><h2>" + (n == 0 ? "Comments" : n + (n == 1 ? " comment" : " comments")) + "</h2><div id=\"ar-clist\">" + CommentList(site, s) + "</div>"
                + "<form class=\"ar-cform\" id=\"ar-cform\" data-story=\"" + Ui.Attr(s.Id) + "\" onsubmit=\"return ArticleJs.comment()\" novalidate>"
                + "<label class=\"ar-field\" for=\"ar-cname\"><span>Name</span><input type=\"text\" id=\"ar-cname\" maxlength=\"40\" autocomplete=\"nickname\"><em class=\"ar-err\" id=\"ar-e-cname\"></em></label>"
                + "<label class=\"ar-field\" for=\"ar-ctext\"><span>Your comment</span><textarea id=\"ar-ctext\" rows=\"4\" maxlength=\"800\"></textarea><em class=\"ar-err\" id=\"ar-e-ctext\"></em></label>"
                + "<div><button type=\"submit\" class=\"ar-btn\">Post comment</button></div><p class=\"ar-hint\">Comments are checked by an editor before they appear.</p><div class=\"ar-cdone\" id=\"ar-cdone\" aria-live=\"polite\"></div></form></section>";
        }

        private static readonly Dictionary<string, List<DateTime>> Recent = new Dictionary<string, List<DateTime>>();

        public async Task<ApiResponse> Comment()
        {
            ApiResponse response = new ApiResponse();
            string name = Ui.Clip(GetDataValue("cname"), 60);
            string text = Ui.Clip(GetDataValue("ctext"), 1200);
            string eName = name.Length < 2 || name.Length > 40 ? "Please enter a name (2 to 40 characters)." : string.Empty;
            string eText = text.Length < 3 ? "Please write a comment." : text.Length > 800 ? "Please keep it under 800 characters." : string.Empty;
            response.SetElementContents("ar-e-cname", eName);
            response.SetElementContents("ar-e-ctext", eText);
            if (eName + eText != string.Empty)
            {
                return response;
            }
            string sid = Ui.Clip(GetDataValue("story"), 120);
            lock (Recent)
            {
                foreach (string k in Recent.Keys.ToList())
                {
                    Recent[k].RemoveAll(t => t < DateTime.UtcNow.AddMinutes(-10));
                    if (Recent[k].Count == 0)
                    {
                        Recent.Remove(k);
                    }
                }
                if (!Recent.TryGetValue(sid, out List<DateTime>? list))
                {
                    list = new List<DateTime>();
                    Recent[sid] = list;
                }
                if (list.Count >= 20)
                {
                    response.SetElementContents("ar-e-ctext", "Lots of comments on this story right now. Please try again in a few minutes.");
                    return response;
                }
                list.Add(DateTime.UtcNow);
            }
            string sbx = await Writable(response, GetDataValue("sbx"));
            using (await Store.Hold())
            {
                SiteData site = await Store.LoadSite(sbx);
                Story? s = site.Stories.FirstOrDefault(x => x.Id == sid && Access.Live(x));
                if (s == null || !site.Settings.Comments || !s.AllowComments)
                {
                    response.SetElementContents("ar-e-ctext", "Comments are closed on this story.");
                    return response;
                }
                if (site.Comments.Count(c => c.Status == "pending") >= 500)
                {
                    response.SetElementContents("ar-e-ctext", "The comment queue is full. Please try again later.");
                    return response;
                }
                site.Comments.Add(new ReaderComment { Id = Guid.NewGuid().ToString("N").Substring(0, 12), Story = s.Id, Name = name, Text = text, When = DateTime.UtcNow, Status = "pending" });
                await Store.SaveSite(site, sbx);
            }
            response.SetElementContents("ar-cdone", "<b>Thanks, " + Ui.Enc(name) + ".</b> Your comment will appear once an editor approves it." + (Store.Demo ? " Demo: sign in to the newsroom to approve it." : string.Empty));
            response.ExecuteScript("ArticleJs.commented();");
            return response;
        }
    }
}
